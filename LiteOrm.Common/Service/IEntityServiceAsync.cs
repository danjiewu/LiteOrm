using LiteOrm.Common;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Service
{
    /// <summary>
    /// 异步版本 - 泛型实体更改接口
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    public interface IEntityServiceAsync<T> : IEntityServiceAsync
    {
        /// <summary>
        /// 异步新增实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果，true表示成功，false表示失败</returns>
        Task<bool> InsertAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步更新实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果，true表示成功，false表示失败</returns>
        Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步更新或新增实体（实体存在则更新，否则新增）
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果，true表示成功，false表示失败</returns>
        Task<bool> UpdateOrInsertAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步删除实体
        /// </summary>
        /// <param name="entity">待删除的实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果，true表示成功，false表示失败</returns>
        Task<bool> DeleteAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步批量新增实体
        /// </summary>
        /// <param name="entities">实体列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        [Transaction]
        Task BatchInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步批量更新实体
        /// </summary>
        /// <param name="entities">实体列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        [Transaction]
        Task BatchUpdateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步批量更新或新增实体（实体存在则更新，否则新增）
        /// </summary>
        /// <param name="entities">实体列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        [Transaction]
        Task BatchUpdateOrInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步批量删除实体
        /// </summary>
        /// <param name="entities">实体列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        [Transaction]
        Task BatchDeleteAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步批量操作实体（操作类型可以为新增、更新或删除）
        /// </summary>
        /// <param name="entities">实体操作列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        [Transaction]
        Task BatchAsync(IEnumerable<EntityOperation<T>> entities, CancellationToken cancellationToken = default);
    }


    /// <summary>
    /// 异步版本 - 非泛型实体更改接口
    /// </summary>
    [AutoRegister(false)]
    public interface IEntityServiceAsync
    {
        /// <summary>
        /// 异步新增实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果，true表示成功，false表示失败</returns>
        Task<bool> InsertAsync(object entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步更新实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果，true表示成功，false表示失败</returns>
        Task<bool> UpdateAsync(object entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步更新或新增实体（实体存在则更新，否则新增）
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果，true表示成功，false表示失败</returns>
        Task<bool> UpdateOrInsertAsync(object entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据ID删除实体
        /// </summary>
        /// <param name="id">待删除id</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果，true表示成功，false表示失败</returns>
        Task<bool> DeleteIDAsync(object id, string[] tableArgs, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件删除实体
        /// </summary>
        /// <param name="expr">删除条件</param>
        /// <param name="tableArgs">表名参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>受影响的行数</returns>
        Task<int> DeleteAsync(Expr expr, string[] tableArgs = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步批量新增实体
        /// </summary>
        /// <param name="entities">实体列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        [Transaction]
        Task BatchInsertAsync(IEnumerable entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步批量更新实体
        /// </summary>
        /// <param name="entities">实体列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        [Transaction]
        Task BatchUpdateAsync(IEnumerable entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步批量更新或新增实体（实体存在则更新，否则新增）
        /// </summary>
        /// <param name="entities">实体列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        [Transaction]
        Task BatchUpdateOrInsertAsync(IEnumerable entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步批量删除实体
        /// </summary>
        /// <param name="entities">实体列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        [Transaction]
        Task BatchDeleteAsync(IEnumerable entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步批量根据ID删除实体
        /// </summary>
        /// <param name="ids">待删除id</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        [Transaction]
        Task BatchDeleteIDAsync(IEnumerable ids, CancellationToken cancellationToken = default);
    }
}
