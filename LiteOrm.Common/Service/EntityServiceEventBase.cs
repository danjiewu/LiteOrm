using LiteOrm.Common;
using System.Collections;
using System.Collections.Generic;

namespace LiteOrm.Service
{
    /// <summary>
    /// 实体服务事件观察者基类，为 <see cref="IEntityServiceEvent{T}"/> 提供默认实现。
    /// </summary>
    /// <remarks>
    /// 继承此类并只重写关心的回调方法即可。除 After（默认空实现）外，Before 系列默认返回
    /// <see langword="true"/>（不取消操作）。
    /// </remarks>
    /// <typeparam name="T">实体类型</typeparam>
    public abstract class EntityServiceEventBase<T> : IEntityServiceEvent<T>
    {
        /// <inheritdoc cref="IEntityServiceEvent{T}.OnInserting"/>
        public virtual bool OnInserting(T entity) => true;

        /// <inheritdoc cref="IEntityServiceEvent{T}.OnUpdating"/>
        public virtual bool OnUpdating(T entity) => true;

        /// <inheritdoc cref="IEntityServiceEvent{T}.OnDeleting"/>
        public virtual bool OnDeleting(T entity) => true;

        /// <inheritdoc cref="IEntityServiceEvent{T}.OnDeleteIDing"/>
        public virtual bool OnDeleteIDing(object id, string[] tableArgs) => true;

        /// <inheritdoc cref="IEntityServiceEvent{T}.OnBatchDeleteIDing"/>
        public virtual bool OnBatchDeleteIDing(IEnumerable ids, string[] tableArgs) => true;

        /// <inheritdoc cref="IEntityServiceEvent{T}.OnDeleteAlling"/>
        public virtual bool OnDeleteAlling(LogicExpr expr, string[] tableArgs) => true;

        /// <inheritdoc cref="IEntityServiceEvent{T}.OnUpdateAlling"/>
        public virtual bool OnUpdateAlling(UpdateExpr expr, string[] tableArgs) => true;

        /// <inheritdoc cref="IEntityServiceEvent{T}.OnInserted"/>
        public virtual void OnInserted(T entity) { }

        /// <inheritdoc cref="IEntityServiceEvent{T}.OnUpdated"/>
        public virtual void OnUpdated(T entity) { }

        /// <inheritdoc cref="IEntityServiceEvent{T}.OnDeleted"/>
        public virtual void OnDeleted(T entity) { }

        /// <inheritdoc cref="IEntityServiceEvent{T}.OnDeleteIDed"/>
        public virtual void OnDeleteIDed(object id, string[] tableArgs) { }

        /// <inheritdoc cref="IEntityServiceEvent{T}.OnBatchDeleteIDed"/>
        public virtual void OnBatchDeleteIDed(IEnumerable ids, string[] tableArgs) { }

        /// <inheritdoc cref="IEntityServiceEvent{T}.OnDeleteAlled"/>
        public virtual void OnDeleteAlled(int count, LogicExpr expr, string[] tableArgs) { }

        /// <inheritdoc cref="IEntityServiceEvent{T}.OnUpdateAlled"/>
        public virtual void OnUpdateAlled(int count, UpdateExpr expr, string[] tableArgs) { }
    }
}