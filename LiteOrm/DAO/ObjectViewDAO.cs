using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
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
    [AutoRegister(ServiceLifetime.Scoped)]
    public class ObjectViewDAO<T> : DAOBase, IObjectViewDAO<T> where T : new()
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

        /// <summary>
        /// <see cref="ObjectViewDAO{T}"/> 为视图DAO，视图DAO不支持增删改操作
        /// </summary>
        protected override bool IsView => true;

        /// <summary>
        /// 使用指定的参数创建新的DAO实例
        /// </summary>
        /// <param name="args">表名参数</param>
        /// <returns>新的DAO实例</returns>
        public ObjectViewDAO<T> WithArgs(params string[] args)
        {
            ObjectViewDAO<T> newDAO = MemberwiseClone() as ObjectViewDAO<T>;
            newDAO.TableArgs = args;
            return newDAO;
        }
        #endregion

        #region 预定义Command
        /// <summary>
        /// 实现获取对象操作的IDbCommand
        /// </summary>
        protected virtual DbCommandProxy MakeGetObjectCommand()
        {
            DbCommandProxy command = NewCommand();
            string where = MakeKeyCondition(command);
            command.CommandText = $"SELECT {AllFieldsSql} \nFROM {From} {ToWhereSql(where)}";
            return command;
        }


        /// <summary>
        /// 实现检查对象是否存在操作的IDbCommand
        /// </summary>
        protected virtual DbCommandProxy MakeObjectExistsCommand()
        {
            ThrowExceptionIfNoKeys();
            DbCommandProxy command = NewCommand();
            StringBuilder strConditions = new StringBuilder();
            foreach (ColumnDefinition key in TableDefinition.Keys)
            {
                if (strConditions.Length != 0) strConditions.Append(" AND ");
                strConditions.AppendFormat("{0} = {1}", ToColumnSql(key), ToSqlParam(key.PropertyName));
                DbParameter param = command.CreateParameter();
                param.Size = key.Length;
                param.DbType = key.DbType;
                param.ParameterName = ToParamName(key.PropertyName);
                command.Parameters.Add(param);
            }
            command.CommandText = $"SELECT 1 \nFROM {FactTableName} {ToWhereSql(strConditions.ToString())}";
            return command;
        }

        #endregion

        #region 常用方法

        /// <summary>
        /// 替换 SQL 中的标记为实际 SQL。
        /// </summary>
        /// <param name="sqlWithParam">包含标记的 SQL 语句，标记可以为 ParamAllFields，ParamFromTable。</param>
        /// <returns>替换后的 SQL 语句。</returns>
        protected override string ReplaceParam(string sqlWithParam)
        {
            return base.ReplaceParam(sqlWithParam).Replace(ParamAllFields, AllFieldsSql);
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
            int count = SelectColumns.Length;
            for (int i = 0; i < count; i++)
            {
                SqlColumn column = SelectColumns[i];
                column.SetValue(t, record.IsDBNull(i) ? null : ConvertFromDbValue(record[i], column.PropertyType));
            }
            return t;
        }


        /// <summary>
        /// 执行 IDbCommand，读取所有记录并转化为对象的集合，查询 AllFieldsSql 时可用
        /// </summary>
        /// <param name="command">待执行的 IDbCommand</param>
        /// <returns></returns>
        protected List<T> GetAll(DbCommandProxy command)
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
        protected List<T> GetAll(DbCommandProxy command, int count)
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
        protected T GetOne(DbCommandProxy command)
        {
            using (IDataReader reader = command.ExecuteReader())
            {
                return ReadOne(reader);
            }
        }

        /// <summary>
        /// 异步读取所有记录并转化为对象集合
        /// </summary>
        private async Task<List<T>> ReadAllAsync(DbDataReader reader, CancellationToken cancellationToken)
        {
            List<T> results = new List<T>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(ConvertToObject(reader));
            }
            return results;
        }

        /// <summary>
        /// 从IDataReader中异步读取一条记录转化为对象
        /// </summary>
        private async Task<T> ReadOneAsync(DbDataReader reader, CancellationToken cancellationToken)
        {
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ConvertToObject(reader) : default(T);
        }


        /// <summary>
        /// 异步执行 IDbCommand，读取所有记录并转化为对象的集合
        /// </summary>
        protected async Task<List<T>> GetAllAsync(DbCommandProxy command, CancellationToken cancellationToken)
        {
            using (DbDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.Default, cancellationToken))
            {
                return await ReadAllAsync(reader, cancellationToken);
            }
        }

        /// <summary>
        /// 异步执行 IDbCommand，读取一条记录并转化为单个对象
        /// </summary>
        protected async Task<T> GetOneAsync(DbCommandProxy command, CancellationToken cancellationToken)
        {
            using (DbDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.Default, cancellationToken))
            {
                return await ReadOneAsync(reader, cancellationToken);
            }
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
            var getObjectCommand = GetPreparedCommand("GetObject", MakeGetObjectCommand);
            int i = 0;
            foreach (DbParameter param in getObjectCommand.Parameters)
            {
                param.Value = ConvertToDbValue(keys[i], TableDefinition.Keys[i].DbType);
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
            var selectExpr = new SelectExpr(expr.ToSource<T>(), Expr.Aggregate("COUNT", Expr.Const(1)));
            using var command = MakeExprCommand(selectExpr);
            return Convert.ToInt32(command.ExecuteScalar());
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
            var objectExistsCommand = GetPreparedCommand("ExistsKey", MakeObjectExistsCommand);
            int i = 0;
            foreach (DbParameter param in objectExistsCommand.Parameters)
            {
                param.Value = ConvertToDbValue(keys[i], TableDefinition.Keys[i].DbType);
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
            var selectExpr = new SelectExpr(expr.ToSource<T>(), Expr.Const(1));
            using var command = MakeExprCommand(selectExpr);
            return command.ExecuteScalar() is not null;
        }

        /// <summary>
        /// 对符合条件的每个对象执行指定操作
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="func">要对每个对象执行的操作</param>
        public void ForEach(Expr expr, Action<T> func)
        {
            using var command = MakeSelectExprCommand(expr);
            using (IDataReader reader = command.ExecuteReader())
            {
                func(ReadOne(reader));
            }
        }

        /// <summary>
        /// 根据条件查询，多个条件以逻辑与连接
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <returns>符合条件的对象列表</returns>
        public virtual List<T> Search(Expr expr)
        {
            using DbCommandProxy command = MakeSelectExprCommand(expr);
            return GetAll(command);
        }

        /// <summary>
        /// 获取单个符合条件的对象
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <returns>第一个符合条件的对象，若不存在则返回null</returns>
        public virtual T SearchOne(Expr expr)
        {
            using var command = MakeSelectExprCommand(expr);
            return GetOne(command);
        }

#if NET8_0_OR_GREATER
        /// <summary>
        /// 根据条件查询，多个条件以逻辑与连接
        /// </summary>
        /// <param name="where">查询条件</param>
        /// <returns>符合条件的对象列表</returns>
        public virtual List<T> Search([InterpolatedStringHandlerArgument("")] ref ExprString where)
        {
            using DbCommandProxy command = MakeSelectExprCommand(where);
            return GetAll(command);
        }

        /// <summary>
        /// 获取单个符合条件的对象
        /// </summary>
        /// <param name="where">查询条件</param>
        /// <returns>第一个符合条件的对象，若不存在则返回null</returns>
        public virtual T SearchOne([InterpolatedStringHandlerArgument("")] ref ExprString where)
        {
            using DbCommandProxy command = MakeSelectExprCommand(where);
            return GetOne(command);
        }
#endif

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
            ThrowExceptionIfWrongKeys(keys);
            var getObjectCommand = GetPreparedCommand("GetObject", MakeGetObjectCommand);
            int i = 0;
            foreach (DbParameter param in getObjectCommand.Parameters)
            {
                param.Value = ConvertToDbValue(keys[i], TableDefinition.Keys[i].DbType);
                i++;
            }
            return await GetOneAsync(getObjectCommand, cancellationToken).ConfigureAwait(false);
        }


        /// <summary>
        /// 异步获取符合条件的对象个数
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含符合条件的对象个数</returns>
        public async virtual Task<int> CountAsync(Expr expr, CancellationToken cancellationToken = default)
        {
            var selectExpr = new SelectExpr(expr.ToSource<T>(), Expr.Aggregate("COUNT", Expr.Const(1)));
            using var command = MakeExprCommand(selectExpr);
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        }

        /// <summary>
        /// 异步判断对象是否存在
        /// </summary>
        /// <param name="o">对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果表示对象是否存在</returns>
        public async virtual Task<bool> ExistsAsync(object o, CancellationToken cancellationToken = default)
        {
            if (o is null) return false;
            return await ExistsKeyAsync(GetKeyValues(o), cancellationToken);
        }

        /// <summary>
        /// 异步判断主键对应的对象是否存在
        /// </summary>
        /// <param name="keys">主键，多个主键按照名称顺序排列</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果表示对象是否存在</returns>
        public async virtual Task<bool> ExistsKeyAsync(object[] keys, CancellationToken cancellationToken = default)
        {
            ThrowExceptionIfWrongKeys(keys);
            var objectExistsCommand = GetPreparedCommand("ExistsKey", MakeObjectExistsCommand);
            int i = 0;
            foreach (DbParameter param in objectExistsCommand.Parameters)
            {
                param.Value = ConvertToDbValue(keys[i], TableDefinition.Keys[i].DbType);
                i++;
            }
            return Convert.ToInt32(await objectExistsCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;
        }


        /// <summary>
        /// 异步判断符合条件的对象是否存在
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果表示对象是否存在</returns>
        public async virtual Task<bool> ExistsAsync(Expr expr, CancellationToken cancellationToken = default)
        {
            var selectExpr = new SelectExpr(expr.ToSource<T>(), Expr.Const(1));
            using var command = MakeExprCommand(selectExpr);
            return await command.ExecuteScalarAsync(cancellationToken) is not null;
        }

        /// <summary>
        /// 异步判断符合条件的对象是否存在（使用表达式）
        /// </summary>
        /// <param name="expression">Lambda表达式条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果表示对象是否存在</returns>
        public async virtual Task<bool> ExistsAsync(Expression<Func<T, bool>> expression, CancellationToken cancellationToken = default)
        {
            return await ExistsAsync(Expr.Exp(expression), cancellationToken);
        }

        /// <summary>
        /// 异步获取单个符合条件的对象
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含第一个符合条件的对象，若不存在则返回null</returns>
        public async virtual Task<T> SearchOneAsync(Expr expr, CancellationToken cancellationToken = default)
        {
            using var command = MakeSelectExprCommand(expr);
            return await GetOneAsync(command, cancellationToken);
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
            var list = await SearchAsync(expr, cancellationToken);
            foreach (var item in list)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await func(item).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 异步根据条件查询，多个条件以逻辑与连接
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含符合条件的对象列表</returns>
        public async virtual Task<List<T>> SearchAsync(Expr expr = null, CancellationToken cancellationToken = default)
        {
            using var command = MakeSelectExprCommand(expr);
            return await GetAllAsync(command, cancellationToken).ConfigureAwait(false);
        }

        #endregion
        #region IObjectViewDAO Members

        /// <summary>
        /// 根据主键获取对象（接口实现）
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <returns>对象，若存在则返回null</returns>
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



        #endregion

        #region IObjectViewDAOAsync implementations

        /// <summary>
        /// 根据主键异步获取对象（接口实现）
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含对象，若不存在则返回null</returns>
        async Task<object> IObjectViewDAOAsync.GetObjectAsync(object[] keys, CancellationToken cancellationToken)
        {
            return await GetObjectAsync(keys, cancellationToken);
        }

        /// <summary>
        /// 异步获取单个符合条件的对象（接口实现）
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含第一个符合条件的对象，若不存在则返回null</returns>
        async Task<object> IObjectViewDAOAsync.SearchOneAsync(Expr expr, CancellationToken cancellationToken)
        {
            return await SearchOneAsync(expr, cancellationToken);
        }

        /// <summary>
        /// 异步根据条件查询，多个条件以逻辑与连接（接口实现）
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含符合条件的对象列表</returns>
        async Task<IList> IObjectViewDAOAsync.SearchAsync(Expr expr, CancellationToken cancellationToken)
        {
            return await SearchAsync(expr, cancellationToken);
        }

#if NET8_0_OR_GREATER

#endif
        #endregion
    }
}
