using LiteOrm.Common;
using System.Collections;
using System.Collections.Generic;

namespace LiteOrm.Service
{
    /// <summary>
    /// 实体服务事件观察者接口。
    /// </summary>
    /// <remarks>
    /// 由订阅者实现并通过依赖注入注册，实体业务服务（<c>EntityService</c>）在插入、更新、删除等操作
    /// 前后调用相应的回调方法。
    ///
    /// 约定：
    /// - Before 系列回调在数据库操作**之前**触发，返回 <see langword="false"/> 表示取消本次操作；
    ///   Before 回调全部返回 <see langword="true"/> 才会继续执行数据库操作。
    /// - After 系列回调在数据库操作**成功之后**触发，用于审计日志、缓存失效、变更通知等副作用。
    ///
    /// 由于本项目需兼容 <c>netstandard2.0</c>，接口成员为抽象方法（无法使用默认接口实现）。
    /// 若只需实现关心的回调，可继承 <see cref="EntityServiceEventBase{T}"/>。
    /// </remarks>
    /// <typeparam name="T">实体类型</typeparam>
    public interface IEntityServiceEvent<T>
    {
        #region 单个实体 - Before

        /// <summary>
        /// 插入前回调。
        /// </summary>
        /// <param name="entity">即将插入的实体。</param>
        /// <returns><see langword="false"/> 表示取消插入。</returns>
        bool OnInserting(T entity);

        /// <summary>
        /// 更新前回调。
        /// </summary>
        /// <param name="entity">即将更新的实体。</param>
        /// <returns><see langword="false"/> 表示取消更新。</returns>
        bool OnUpdating(T entity);

        /// <summary>
        /// 更新或插入（upsert）前回调。
        /// </summary>
        /// <remarks>
        /// 针对 <c>UpdateOrInsert</c> 的专用钩子；无论最终是更新还是插入，操作执行前都会触发。
        /// 与 <see cref="OnUpdating"/> 并存：仅关心 upsert 的下游可订阅此钩子。
        /// </remarks>
        /// <param name="entity">即将更新或插入的实体。</param>
        /// <returns><see langword="false"/> 表示取消本次更新或插入。</returns>
        bool OnUpdatingOrInserting(T entity);

        /// <summary>
        /// 删除前回调。
        /// </summary>
        /// <param name="entity">即将删除的实体。</param>
        /// <returns><see langword="false"/> 表示取消删除。</returns>
        bool OnDeleting(T entity);

        #endregion

        #region 单个实体 - After

        /// <summary>
        /// 插入成功后回调。
        /// </summary>
        /// <param name="entity">已插入的实体。</param>
        void OnInserted(T entity);

        /// <summary>
        /// 更新成功后回调。
        /// </summary>
        /// <param name="entity">已更新的实体。</param>
        void OnUpdated(T entity);

        /// <summary>
        /// 更新或插入（upsert）成功后回调。
        /// </summary>
        /// <remarks>
        /// 针对 <c>UpdateOrInsert</c> 的专用钩子；无论最终是更新还是插入，操作成功后都会触发。
        /// </remarks>
        /// <param name="entity">已更新或插入的实体。</param>
        void OnUpdatedOrInserted(T entity);

        /// <summary>
        /// 删除成功后回调。
        /// </summary>
        /// <param name="entity">已删除的实体。</param>
        void OnDeleted(T entity);

        #endregion

        #region ID 删除 - Before

        /// <summary>
        /// 按 ID 删除前回调。
        /// </summary>
        /// <param name="id">待删除的主键值。</param>
        /// <param name="tableArgs">表名参数。</param>
        /// <returns><see langword="false"/> 表示取消删除。</returns>
        bool OnDeleteIDing(object id, string[] tableArgs);

        /// <summary>
        /// 按 ID 批量删除前回调。
        /// </summary>
        /// <param name="ids">待删除的主键值集合。</param>
        /// <param name="tableArgs">表名参数。</param>
        /// <returns><see langword="false"/> 表示取消本次批量删除。</returns>
        bool OnBatchDeleteIDing(IEnumerable ids, string[] tableArgs);

        #endregion

        #region ID 删除 - After

        /// <summary>
        /// 按 ID 删除成功后回调。
        /// </summary>
        /// <param name="id">已删除的主键值。</param>
        /// <param name="tableArgs">表名参数。</param>
        void OnDeleteIDed(object id, string[] tableArgs);

        /// <summary>
        /// 按 ID 批量删除成功后回调。
        /// </summary>
        /// <param name="ids">已删除的主键值集合。</param>
        /// <param name="tableArgs">表名参数。</param>
        void OnBatchDeleteIDed(IEnumerable ids, string[] tableArgs);

        #endregion

        #region 条件 - Before

        /// <summary>
        /// 按条件删除前回调。
        /// </summary>
        /// <param name="expr">删除条件表达式。</param>
        /// <param name="tableArgs">表名参数。</param>
        /// <returns><see langword="false"/> 表示取消删除。</returns>
        bool OnDeleteAlling(LogicExpr expr, string[] tableArgs);

        /// <summary>
        /// 按表达式更新前回调。
        /// </summary>
        /// <param name="expr">更新表达式。</param>
        /// <param name="tableArgs">表名参数。</param>
        /// <returns><see langword="false"/> 表示取消更新。</returns>
        bool OnUpdateAlling(UpdateExpr expr, string[] tableArgs);

        #endregion

        #region 条件 - After

        /// <summary>
        /// 按条件删除成功后回调。
        /// </summary>
        /// <param name="count">受影响的行数。</param>
        /// <param name="expr">删除条件表达式。</param>
        /// <param name="tableArgs">表名参数。</param>
        void OnDeleteAlled(int count, LogicExpr expr, string[] tableArgs);

        /// <summary>
        /// 按表达式更新成功后回调。
        /// </summary>
        /// <param name="count">受影响的行数。</param>
        /// <param name="expr">更新表达式。</param>
        /// <param name="tableArgs">表名参数。</param>
        void OnUpdateAlled(int count, UpdateExpr expr, string[] tableArgs);

        #endregion
    }
}