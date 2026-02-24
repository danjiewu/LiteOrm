using Autofac.Extras.DynamicProxy;
using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Service
{
    /// <summary>
    /// 视图实体业务服务类 - 提供视图实体的查询和通用业务操作
    /// </summary>
    /// <typeparam name="TView">视图实体类型</typeparam>
    /// <remarks>
    /// EntityViewService&lt;TView&gt; 是一个业务服务类，专门用于视图实体的查询和读取操作。
    /// 视图通常由一个或多个表的联接组成，提供了一种便捷的方式来获取相关的数据。
    /// 
    /// 主要功能包括：
    /// 1. 单对象查询 - GetObject() 方法根据主键获取单个视图实体
    /// 2. 存在性检查 - Exists() 和 ExistsID() 方法检查实体是否存在
    /// 3. 列表查询 - Search() 方法获取符合条件的实体列表
    /// 4. 统计操作 - Count() 方法用于计数
    /// 5. 异步支持 - 提供基于 Task 的异步方法
    /// 6. 灵活的条件 - 支持使用 Expr 对象或 Lambda 表达式进行条件查询
    /// 7. 表参数支持 - 支持通过 tableArgs 参数动态指定表名
    /// 8. 拦截机制 - 自动应用 ServiceInvokeInterceptor 进行拦截
    /// 
    /// 该类通过依赖注入框架以单例方式注册，使用 Autofac 的拦截功能进行方法拦截。
    /// 
    /// 使用示例：
    /// <code>
    /// var service = serviceProvider.GetRequiredService&lt;IEntityViewService&lt;UserView&gt;&gt;();
    /// 
    /// // 获取单个实体
    /// var user = service.GetObject(userId);
    /// 
    /// // 检查实体是否存在
    /// bool exists = service.Exists(Expr.Property("Username") == "john.doe");
    /// 
    /// // 获取列表
    /// var users = service.Search(Expr.Property("Age") > 18);
    /// 
    /// // 异步查询
    /// var userAsync = await service.GetObjectAsync(userId);
    /// </code>
    /// </remarks>
    [AutoRegister(ServiceLifetime.Scoped)]
    [Intercept(typeof(ServiceInvokeInterceptor))]
    public class EntityViewService<TView> : IEntityViewService<TView>, IEntityViewServiceAsync<TView>, IEntityViewService, IEntityViewServiceAsync
         where TView : new()
    {
        /// <summary>
        /// 获取或设置用于视图查询的数据访问对象。
        /// </summary>
        public ObjectViewDAO<TView> ObjectViewDAO { get; set; }

        #region IEntityViewService<TView> 成员

        /// <summary>
        /// 获取视图类型
        /// </summary>
        public Type ViewType
        {
            get { return typeof(TView); }
        }

        /// <summary>
        /// 根据ID获取视图对象
        /// </summary>
        /// <param name="id">对象ID</param>
        /// <param name="tableArgs">表参数</param>
        /// <returns>视图对象，若不存在则返回null</returns>
        public virtual TView GetObject(object id, params string[] tableArgs)
        {
            return ObjectViewDAO.WithArgs(tableArgs).GetObject(id);
        }

        /// <summary>
        /// 判断指定ID的对象是否存在
        /// </summary>
        /// <param name="id">对象ID</param>
        /// <param name="tableArgs">表参数</param>
        /// <returns>是否存在</returns>
        public virtual bool ExistsID(object id, params string[] tableArgs)
        {
            return ObjectViewDAO.WithArgs(tableArgs).Exists(new object[] { id });
        }

        /// <summary>
        /// 判断符合条件的对象是否存在
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="tableArgs">表参数</param>
        /// <returns>是否存在</returns>
        public virtual bool Exists(Expr expr, params string[] tableArgs)
        {
            return ObjectViewDAO.WithArgs(tableArgs).Exists(expr);
        }

        /// <summary>
        /// 使用Lambda表达式判断对象是否存在
        /// </summary>
        /// <param name="expression">Lambda表达式条件</param>
        /// <param name="tableArgs">表参数</param>
        /// <returns>是否存在</returns>
        public virtual bool Exists(Expression<Func<TView, bool>> expression, params string[] tableArgs)
        {
            return ObjectViewDAO.WithArgs(tableArgs).Exists(expression);
        }

        /// <summary>
        /// 获取符合条件的对象个数
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="tableArgs">表参数</param>
        /// <returns>符合条件的对象个数</returns>
        public virtual int Count(Expr expr = null, params string[] tableArgs)
        {
            return ObjectViewDAO.WithArgs(tableArgs).Count(expr);
        }

        /// <summary>
        /// 对符合条件的每个对象执行指定操作
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="func">要执行的操作</param>
        /// <param name="tableArgs">表参数</param>
        public virtual void ForEach(Expr expr, Action<TView> func, params string[] tableArgs)
        {
            ObjectViewDAO.WithArgs(tableArgs).ForEach(expr, func);
        }

        /// <summary>
        /// 获取单个符合条件的视图对象
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="tableArgs">表参数</param>
        /// <returns>第一个符合条件的视图对象，若不存在则返回null</returns>
        public virtual TView SearchOne(Expr expr, params string[] tableArgs)
        {
            return ObjectViewDAO.WithArgs(tableArgs).SearchOne(expr);
        }

        /// <summary>
        /// 根据条件查询视图对象列表
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="tableArgs">表参数</param>
        /// <returns>符合条件的视图对象列表</returns>
        public virtual List<TView> Search(Expr expr = null, params string[] tableArgs)
        {
            return ObjectViewDAO.WithArgs(tableArgs).Search(expr);
        }

        #endregion

        #region IEntityViewService 成员

        object IEntityViewService.GetObject(object id, params string[] tableArgs)
        {
            return GetObject(id, tableArgs);
        }

        object IEntityViewService.SearchOne(Expr expr, params string[] tableArgs)
        {
            return SearchOne(expr, tableArgs);
        }

        IList IEntityViewService.Search(Expr expr, params string[] tableArgs)
        {
            return Search(expr, tableArgs);
        }

        #endregion

        #region IEntityViewServiceAsync 成员

        async Task<object> IEntityViewServiceAsync.GetObjectAsync(object id, string[] tableArgs, CancellationToken cancellationToken)
        {
            return await ObjectViewDAO.WithArgs(tableArgs).GetObjectAsync(new object[] { id }, cancellationToken);
        }

        async Task<object> IEntityViewServiceAsync.SearchOneAsync(Expr expr, string[] tableArgs, CancellationToken cancellationToken)
        {
            return await ObjectViewDAO.WithArgs(tableArgs).SearchOneAsync(expr, cancellationToken);
        }

        async Task<IList> IEntityViewServiceAsync.SearchAsync(Expr expr, string[] tableArgs, CancellationToken cancellationToken)
        {
            return await ObjectViewDAO.WithArgs(tableArgs).SearchAsync(expr, cancellationToken);
        }
        #endregion

        #region IEntityViewServiceAsync<TView> 成员

        /// <summary>
        /// 异步根据ID获取视图对象
        /// </summary>
        /// <param name="id">对象ID</param>
        /// <param name="tableArgs">表参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>视图对象，若不存在则返回null</returns>
        public async virtual Task<TView> GetObjectAsync(object id, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return await ObjectViewDAO.WithArgs(tableArgs).GetObjectAsync(new object[] { id }, cancellationToken);
        }

        /// <summary>
        /// 异步判断指定ID的对象是否存在
        /// </summary>
        /// <param name="id">对象ID</param>
        /// <param name="tableArgs">表参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否存在</returns>
        public async virtual Task<bool> ExistsIDAsync(object id, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return await ObjectViewDAO.WithArgs(tableArgs).ExistsKeyAsync(new object[] { id }, cancellationToken);
        }

        /// <summary>
        /// 异步判断符合条件的对象是否存在
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="tableArgs">表参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否存在</returns>
        public async virtual Task<bool> ExistsAsync(Expr expr, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return await ObjectViewDAO.WithArgs(tableArgs).ExistsAsync(expr, cancellationToken);
        }

        /// <summary>
        /// 异步使用Lambda表达式判断对象是否存在
        /// </summary>
        /// <param name="expression">Lambda表达式条件</param>
        /// <param name="tableArgs">表参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否存在</returns>
        public async virtual Task<bool> ExistsAsync(Expression<Func<TView, bool>> expression, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return await ObjectViewDAO.WithArgs(tableArgs).ExistsAsync(expression, cancellationToken);
        }

        /// <summary>
        /// 异步获取符合条件的对象个数
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="tableArgs">表参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>符合条件的对象个数</returns>
        public async virtual Task<int> CountAsync(Expr expr = null, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return await ObjectViewDAO.WithArgs(tableArgs).CountAsync(expr, cancellationToken);
        }

        /// <summary>
        /// 异步对符合条件的每个对象执行指定的操作。
        /// </summary>
        /// <param name="expr">查询条件。</param>
        /// <param name="func">要执行的异步操作。</param>
        /// <param name="tableArgs">表名参数。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>返回异步操作任务。</returns>
        public async virtual Task ForEachAsync(Expr expr, Func<TView, Task> func, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            await ObjectViewDAO.WithArgs(tableArgs).ForEachAsync(expr, func, cancellationToken);
        }

        /// <summary>
        /// 异步获取单个符合条件的视图对象
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="tableArgs">表参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>第一个符合条件的视图对象，若不存在则返回null</returns>
        public async virtual Task<TView> SearchOneAsync(Expr expr, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return await ObjectViewDAO.WithArgs(tableArgs).SearchOneAsync(expr, cancellationToken);
        }

        /// <summary>
        /// 异步查询符合条件的实体列表。
        /// </summary>
        /// <param name="expr">查询条件。</param>
        /// <param name="tableArgs">表名参数。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>实体列表结果。</returns>
        public async virtual Task<List<TView>> SearchAsync(Expr expr = null, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return await ObjectViewDAO.WithArgs(tableArgs).SearchAsync(expr, cancellationToken);
        }

        #endregion
    }
}