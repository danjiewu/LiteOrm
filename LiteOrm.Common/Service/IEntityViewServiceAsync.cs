using LiteOrm.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Service
{
    /// <summary>
    /// 异步版本 - 泛型实体查询接口
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    public interface IEntityViewServiceAsync<T> : IEntityViewServiceAsync
    {
        /// <summary>
        /// 异步获取实体
        /// </summary>
        /// <param name="id">实体主键</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>id对应实体，若不存在则返回null</returns>
        new Task<T> GetObjectAsync(object id, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件获取单个实体
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>第一个符合条件的实体，若不存在则返回null</returns>
        new Task<T> SearchOneAsync(Expr expr, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件遍历对象
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="func">调用的异步函数委托</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        Task ForEachAsync(Expr expr, Func<T, Task> func, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件获取实体列表
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>符合条件的实体列表</returns>
        new Task<List<T>> SearchAsync(Expr expr = null, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件查询实体列表，并指定排序项
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="orderby">排序项</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>符合条件的实体列表</returns>
        new Task<List<T>> SearchWithOrderAsync(Expr expr, Sorting[] orderby, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件分页查询
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="section">分页设置</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>符合条件的分页对象列表</returns>
        new Task<List<T>> SearchSectionAsync(Expr expr, PageSection section, string[] tableArgs = null, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 异步版本 - 非泛型实体查询接口
    /// </summary>
    [AutoRegister(false)]
    public interface IEntityViewServiceAsync
    {
        /// <summary>
        /// 异步获取实体
        /// </summary>
        /// <param name="id">实体主键</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>id对应实体，若不存在则返回null</returns>
        Task<object> GetObjectAsync(object id, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步检测ID是否存在
        /// </summary>
        /// <param name="id">实体主键</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否存在记录</returns>
        Task<bool> ExistsIDAsync(object id, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件检查是否存在记录
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否存在记录</returns>
        Task<bool> ExistsAsync(Expr expr, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件获取记录总数
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>符合条件的记录总数</returns>
        Task<int> CountAsync(Expr expr = null, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件获取单个实体
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>第一个符合条件的实体，若不存在则返回null</returns>
        Task<object> SearchOneAsync(Expr expr, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件获取实体列表
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>符合条件的实体列表</returns>
        Task<IList> SearchAsync(Expr expr = null, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件查询实体列表，并指定排序项
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="orderby">排序项</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>符合条件的实体列表</returns>
        Task<IList> SearchWithOrderAsync(Expr expr, Sorting[] orderby, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件分页查询
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <param name="section">分页设置</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="tableArgs">表名参数</param>
        /// 
        /// <returns>符合条件的分页对象列表</returns>
        Task<IList> SearchSectionAsync(Expr expr, PageSection section, string[] tableArgs = null, CancellationToken cancellationToken = default);
    }
}
