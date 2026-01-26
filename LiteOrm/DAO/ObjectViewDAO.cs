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
using Microsoft.Extensions.DependencyInjection;

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
    public partial class ObjectViewDAO<T> : DAOBase, IObjectViewDAO<T> where T : new()
    {
        #region 方法

        /// <summary>
        /// 根据主键获取对象
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <returns>对象，若不存在则返回null</returns>
        public virtual T GetObject(params object[] keys)
        {
            ThrowExceptionIfWrongKeys(keys);
            var getObjectCommand = DAOContext.PreparedCommands.GetOrAdd((ObjectType, "GetObject"), _ => MakeGetObjectCommand());
            int i = 0;
            foreach (IDataParameter param in getObjectCommand.Parameters)
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
            using var command = MakeConditionCommand($"select count(*) \nfrom {ParamFromTable} {ParamWhere}", expr);
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
            var objectExistsCommand = DAOContext.PreparedCommands.GetOrAdd((ObjectType, "ExistsKey"), _ => MakeObjectExistsCommand());
            int i = 0;
            foreach (IDataParameter param in objectExistsCommand.Parameters)
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
            using var command = MakeConditionCommand($"select 1 \nfrom {ParamFromTable} {ParamWhere}", expr);
            return command.ExecuteScalar() is not null;
        }

        /// <summary>
        /// 对符合条件的每个对象执行指定操作
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="func">要对每个对象执行的操作</param>
        public void ForEach(Expr expr, Action<T> func)
        {
            using var command = MakeConditionCommand($"select {ParamAllFields} \nfrom {ParamFromTable} {ParamWhere}", expr);
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
            using var command = MakeConditionCommand($"select {ParamAllFields} \nfrom {ParamFromTable} {ParamWhere}", expr);
            return GetAll(command);
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
            {
                using var command = MakeConditionCommand($"select {ParamAllFields} \nfrom {ParamFromTable} {ParamWhere} order by " + GetOrderBySql(orderBy), expr);
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
            using var command = MakeConditionCommand($"select {ParamAllFields} \nfrom {ParamFromTable} {ParamWhere}", expr);
            return GetOne(command);
        }


        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="section">分页设定</param>
        /// <returns></returns>
        public virtual List<T> SearchSection(Expr expr, PageSection section)
        {
            string sql = SqlBuilder.GetSelectSectionSql(AllFieldsSql, From, ParamWhere, GetOrderBySql(section.Orders), section.StartIndex, section.SectionSize);
            using var command = MakeConditionCommand(sql, expr);
            return GetAll(command);
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
            ThrowExceptionIfWrongKeys(keys);
            var getObjectCommand = DAOContext.PreparedCommands.GetOrAdd((ObjectType, "GetObject"), _ => MakeGetObjectCommand());
            int i = 0;
            foreach (IDataParameter param in getObjectCommand.Parameters)
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
            using var command = MakeConditionCommand($"select count(*) \nfrom {ParamFromTable} {ParamWhere}", expr);
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
            var objectExistsCommand = DAOContext.PreparedCommands.GetOrAdd((ObjectType, "ExistsKey"), _ => MakeObjectExistsCommand());
            int i = 0;
            foreach (IDataParameter param in objectExistsCommand.Parameters)
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
            using var command = MakeConditionCommand($"select 1 \nfrom {ParamFromTable} {ParamWhere}", expr);
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
            using var command = MakeConditionCommand($"select {ParamAllFields} \nfrom {ParamFromTable} {ParamWhere}", expr);
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
            using var command = MakeConditionCommand($"select {ParamAllFields} \nfrom {ParamFromTable} {ParamWhere}", expr);
            return await GetAllAsync(command, cancellationToken).ConfigureAwait(false);
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
            if (orderBy is null || orderBy.Length == 0) return await SearchAsync(expr, cancellationToken);

            using var command = MakeConditionCommand($"select {ParamAllFields} \nfrom {ParamFromTable} {ParamWhere} order by " + GetOrderBySql(orderBy), expr);
            return await GetAllAsync(command, cancellationToken);
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
            string sql = SqlBuilder.GetSelectSectionSql(AllFieldsSql, From, ParamWhere, GetOrderBySql(section.Orders), section.StartIndex, section.SectionSize);
            using var command = MakeConditionCommand(sql, expr);
            return await GetAllAsync(command, cancellationToken);
        }

        #endregion
    }
}
