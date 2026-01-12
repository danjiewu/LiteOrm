using Microsoft.Extensions.DependencyInjection;
using MyOrm.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace MyOrm.Service
{
    /// <summary>
    /// 实体业务服务类 - 提供实体类的增删改查等业务操作
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <typeparam name="TView">实体视图类型，用于查询操作，必须继承自T</typeparam>
    /// <remarks>
    /// EntityService&lt;T, TView&gt; 是一个业务服务类，提供对实体类型的完整业务操作支持。
    /// 
    /// 主要功能包括：
    /// 1. 插入操作 - Insert() 和 InsertAsync() 方法用于插入新的实体
    /// 2. 更新操作 - Update() 和 UpdateAsync() 方法用于更新现有的实体
    /// 3. 删除操作 - Delete() 和 DeleteAsync() 方法用于删除指定的实体
    /// 4. 批量操作 - Batch()、BatchInsert()、BatchUpdate() 等批量操作方法
    /// 5. 字段级更新 - UpdateValues() 方法允许更新指定的字段
    /// 6. 异步支持 - 提供基于 Task 的异步方法以支持异步编程
    /// 7. 事务支持 - 通过 SessionManager 支持事务处理
    /// 8. 查询操作 - 继承自 EntityViewService，提供各种查询能力
    /// 
    /// 该类继承自 EntityViewService&lt;TView&gt; 并实现了 IEntityService&lt;T&gt; 和 IEntityServiceAsync&lt;T&gt; 接口，
    /// 提供强类型的业务服务。
    /// 
    /// 使用示例：
    /// <code>
    /// var service = serviceProvider.GetRequiredService&lt;IEntityService&lt;User&gt;&gt;();
    /// 
    /// // 插入实体
    /// var user = new User { Name = "John", Email = "john@example.com" };
    /// bool inserted = service.Insert(user);
    /// 
    /// // 异步插入
    /// bool insertedAsync = await service.InsertAsync(user);
    /// 
    /// // 更新实体
    /// user.Name = "Jane";
    /// bool updated = service.Update(user);
    /// 
    /// // 删除实体
    /// bool deleted = service.Delete(user.Id);
    /// 
    /// // 批量操作
    /// var users = new[] { user1, user2, user3 };
    /// await service.BatchInsertAsync(users);
    /// </code>
    /// </remarks>
    public class EntityService<T, TView> : EntityViewService<TView>, IEntityService<T>, IEntityServiceAsync<T>, IEntityService, IEntityServiceAsync
    where TView : T, new()
    where T : new()
    {
        /// <summary>
        /// 获取或设置实体数据访问对象。
        /// </summary>
        public IObjectDAO<T> ObjectDAO { get; set; }

        #region IEntityService<T> 成员

        /// <summary>
        /// 获取实体类型
        /// </summary>
        public Type EntityType
        {
            get { return typeof(T); }
        }

        /// <summary>
        /// 插入实体
        /// </summary>
        /// <param name="entity">要插入的实体</param>
        /// <returns>是否插入成功</returns>
        public virtual bool Insert(T entity)
        {
            return InsertCore(entity);
        }

        /// <summary>
        /// 更新实体
        /// </summary>
        /// <param name="entity">要更新的实体</param>
        /// <returns>是否更新成功</returns>
        public virtual bool Update(T entity)
        {
            return UpdateCore(entity);
        }

        /// <summary>
        /// 根据条件更新多个字段的值
        /// </summary>
        /// <param name="updateValues">要更新的字段及其值</param>
        /// <param name="expr">更新条件</param>
        /// <returns>更新的记录数</returns>
        public virtual int UpdateValues(IEnumerable<KeyValuePair<string, object>> updateValues, Expr expr)
        {
            return ObjectDAO.UpdateAllValues(updateValues, expr);
        }

        /// <summary>
        /// 根据主键更新多个字段的值
        /// </summary>
        /// <param name="updateValues">要更新的字段及其值</param>
        /// <param name="keys">主键值</param>
        /// <returns>是否更新成功</returns>
        public virtual bool UpdateValues(IEnumerable<KeyValuePair<string, object>> updateValues, params object[] keys)
        {
            return ObjectDAO.UpdateValues(updateValues, keys);
        }

        /// <summary>
        /// 批量处理实体操作（插入、更新、删除）
        /// </summary>
        /// <param name="entities">实体操作集合</param>
        public virtual void Batch(IEnumerable<EntityOperation<T>> entities)
        {
            foreach (EntityOperation<T> entityOp in entities)
            {
                switch (entityOp.Operation)
                {
                    case OpDef.Insert:
                        InsertCore(entityOp.Entity);
                        break;
                    case OpDef.Update:
                        UpdateCore(entityOp.Entity);
                        break;
                    case OpDef.Delete:
                        DeleteCore(entityOp.Entity);
                        break;
                }
            }
        }

        /// <summary>
        /// 异步批量处理实体操作（插入、更新、删除）
        /// </summary>
        /// <param name="entities">实体操作集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public virtual Task BatchAsync(IEnumerable<EntityOperation<T>> entities, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => Batch(entities), cancellationToken);
        }

        /// <summary>
        /// 异步批量插入实体
        /// </summary>
        /// <param name="entities">实体集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public virtual Task BatchInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => BatchInsert(entities), cancellationToken);
        }

        /// <summary>
        /// 异步批量更新实体
        /// </summary>
        /// <param name="entities">实体集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public virtual Task BatchUpdateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => BatchUpdate(entities), cancellationToken);
        }

        /// <summary>
        /// 异步批量更新或插入实体
        /// </summary>
        /// <param name="entities">实体集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public virtual Task BatchUpdateOrInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => BatchUpdateOrInsert(entities), cancellationToken);
        }

        /// <summary>
        /// 异步批量删除实体
        /// </summary>
        /// <param name="entities">实体集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public virtual Task BatchDeleteAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => BatchDelete(entities), cancellationToken);
        }

        /// <summary>
        /// 异步批量根据ID删除实体
        /// </summary>
        /// <param name="ids">ID集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public virtual Task BatchDeleteIDAsync(IEnumerable ids, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => BatchDeleteID(ids), cancellationToken);
        }

        /// <summary>
        /// 异步插入实体
        /// </summary>
        /// <param name="entity">要插入的实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否插入成功</returns>
        public virtual Task<bool> InsertAsync(T entity, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => Insert(entity), cancellationToken);
        }

        /// <summary>
        /// 异步更新实体
        /// </summary>
        /// <param name="entity">要更新的实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否更新成功</returns>
        public virtual Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => Update(entity), cancellationToken);
        }

        /// <summary>
        /// 异步更新或插入实体
        /// </summary>
        /// <param name="entity">要更新或插入的实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否操作成功</returns>
        public virtual Task<bool> UpdateOrInsertAsync(T entity, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => UpdateOrInsert(entity), cancellationToken);
        }

        /// <summary>
        /// 更新或插入实体
        /// </summary>
        /// <param name="entity">要更新或插入的实体</param>
        /// <returns>是否操作成功</returns>
        public virtual bool UpdateOrInsert(T entity)
        {
            switch (UpdateOrInsertCore(entity))
            {
                case UpdateOrInsertResult.Inserted:
                case UpdateOrInsertResult.Updated:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 根据ID删除实体
        /// </summary>
        /// <param name="id">实体ID</param>
        /// <returns>是否删除成功</returns>
        public virtual bool DeleteID(object id)
        {
            return DeleteIDCore(id);
        }

        /// <summary>
        /// 删除实体
        /// </summary>
        /// <param name="entity">要删除的实体</param>
        /// <returns>是否删除成功</returns>
        public virtual bool Delete(T entity)
        {
            return DeleteCore(entity);
        }

        /// <summary>
        /// 根据条件删除实体
        /// </summary>
        /// <param name="expr">删除条件</param>
        /// <returns>删除的记录数</returns>
        public virtual int Delete(Expr expr)
        {
            return ObjectDAO.Delete(expr);
        }

        /// <summary>
        /// 批量插入实体
        /// </summary>
        /// <param name="entities">实体集合</param>
        public virtual void BatchInsert(IEnumerable<T> entities)
        {
            if (typeof(IArged).IsAssignableFrom(typeof(T)))
            {
                var groups = entities.ToLookup(t => ((IArged)t).TableArgs, StringArrayEqualityComparer.Instance);
                foreach (var group in groups)
                {
                    ObjectDAO.WithArgs(group.Key).BatchInsert(group);
                }
            }
            else
                ObjectDAO.BatchInsert(entities);
        }

        /// <summary>
        /// 批量更新实体
        /// </summary>
        /// <param name="entities">实体集合</param>
        public virtual void BatchUpdate(IEnumerable<T> entities)
        {
            foreach (T entity in entities)
            {
                UpdateCore(entity);
            }
        }

        /// <summary>
        /// 批量更新或插入实体
        /// </summary>
        /// <param name="entities">实体集合</param>
        public virtual void BatchUpdateOrInsert(IEnumerable<T> entities)
        {
            foreach (T entity in entities)
            {
                UpdateOrInsert(entity);
            }
        }

        /// <summary>
        /// 批量删除实体
        /// </summary>
        /// <param name="entities">实体集合</param>
        public virtual void BatchDelete(IEnumerable<T> entities)
        {
            foreach (T entity in entities)
            {
                DeleteCore(entity);
            }
        }

        /// <summary>
        /// 批量根据ID删除实体
        /// </summary>
        /// <param name="ids">ID集合</param>
        public virtual void BatchDeleteID(IEnumerable ids)
        {
            foreach (object id in ids)
            {
                DeleteIDCore(id);
            }
        }

        #endregion

        #region NoNotify Methods

        /// <summary>
        /// 核心插入逻辑。
        /// </summary>
        /// <param name="entity">实体对象。</param>
        /// <returns>是否插入成功。</returns>
        protected virtual bool InsertCore(T entity)
        {
            if (entity is IArged arg)
                return ObjectDAO.WithArgs(arg.TableArgs).Insert(entity);
            return ObjectDAO.Insert(entity);
        }

        /// <summary>
        /// 核心更新逻辑。
        /// </summary>
        /// <param name="entity">实体对象。</param>
        /// <returns>是否更新成功。</returns>
        protected virtual bool UpdateCore(T entity)
        {
            if (entity is IArged arg)
                return ObjectDAO.WithArgs(arg.TableArgs).Update(entity);
            return ObjectDAO.Update(entity);
        }

        /// <summary>
        /// 核心更新或插入逻辑。
        /// </summary>
        /// <param name="entity">实体对象。</param>
        /// <returns>操作结果。</returns>
        protected virtual UpdateOrInsertResult UpdateOrInsertCore(T entity)
        {
            bool exists;
            if (entity is IArged arg)
            {
                exists = ObjectViewDAO.WithArgs(arg.TableArgs).Exists(entity);
            }
            else
            {
                exists = ObjectViewDAO.Exists(entity);
            }

            if (exists)
                return UpdateCore(entity) ? UpdateOrInsertResult.Updated : UpdateOrInsertResult.Failed;
            else
                return InsertCore(entity) ? UpdateOrInsertResult.Inserted : UpdateOrInsertResult.Failed;
        }

        /// <summary>
        /// 核心基于 ID 的删除逻辑。
        /// </summary>
        /// <param name="id">实体主键。</param>
        /// <returns>是否删除成功。</returns>
        protected virtual bool DeleteIDCore(object id)
        {
            return ObjectDAO.DeleteByKeys(id);
        }

        /// <summary>
        /// 核心删除逻辑。
        /// </summary>
        /// <param name="entity">实体对象。</param>
        /// <returns>是否删除成功。</returns>
        protected virtual bool DeleteCore(T entity)
        {
            if (entity is IArged arg)
                return ObjectDAO.WithArgs(arg.TableArgs).Delete(entity);
            return ObjectDAO.Delete(entity);
        }
        #endregion

        #region IEntityService 成员
        bool IEntityService.Insert(object entity)
        {
            return Insert((T)entity);
        }

        bool IEntityService.Update(object entity)
        {
            return Update((T)entity);
        }


        bool IEntityService.UpdateOrInsert(object entity)
        {
            return UpdateOrInsert((T)entity);
        }

        void IEntityService.BatchInsert(IEnumerable entities)
        {
            if (entities is IEnumerable<T> typed)
                BatchInsert(typed);
            else
            {
                var list = new List<T>();
                foreach (object entity in entities)
                {
                    list.Add((T)entity);
                }
                BatchInsert(list);
            }
        }

        void IEntityService.BatchUpdate(IEnumerable entities)
        {
            if (entities is IEnumerable<T> typed)
                BatchUpdate(typed);
            else
            {
                var list = new List<T>();
                foreach (object entity in entities)
                {
                    list.Add((T)entity);
                }
                BatchUpdate(list);
            }
        }

        void IEntityService.BatchUpdateOrInsert(IEnumerable entities)
        {
            if (entities is IEnumerable<T> typed)
                BatchUpdateOrInsert(typed);
            else
            {
                var list = new List<T>();
                foreach (object entity in entities)
                {
                    list.Add((T)entity);
                }
                BatchUpdateOrInsert(list);
            }
        }

        void IEntityService.BatchDelete(IEnumerable entities)
        {
            if (entities is IEnumerable<T> typed)
                BatchDelete(typed);
            else
            {
                var list = new List<T>();
                foreach (object entity in entities)
                {
                    list.Add((T)entity);
                }
                BatchDelete(list);
            }
        }

        #endregion

        #region IEntityServiceAsync 成员

        Task<bool> IEntityServiceAsync.InsertAsync(object entity, CancellationToken cancellationToken)
        {
            return InsertAsync((T)entity, cancellationToken);
        }

        Task<bool> IEntityServiceAsync.UpdateAsync(object entity, CancellationToken cancellationToken)
        {
            return UpdateAsync((T)entity, cancellationToken);
        }

        Task<bool> IEntityServiceAsync.UpdateOrInsertAsync(object entity, CancellationToken cancellationToken)
        {
            return UpdateOrInsertAsync((T)entity, cancellationToken);
        }

        Task IEntityServiceAsync.BatchInsertAsync(IEnumerable entities, CancellationToken cancellationToken)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => ((IEntityService)this).BatchInsert(entities), cancellationToken);
        }

        Task IEntityServiceAsync.BatchUpdateAsync(IEnumerable entities, CancellationToken cancellationToken)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => ((IEntityService)this).BatchUpdate(entities), cancellationToken);
        }

        Task IEntityServiceAsync.BatchUpdateOrInsertAsync(IEnumerable entities, CancellationToken cancellationToken)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => ((IEntityService)this).BatchUpdateOrInsert(entities), cancellationToken);
        }

        Task IEntityServiceAsync.BatchDeleteAsync(IEnumerable entities, CancellationToken cancellationToken)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => ((IEntityService)this).BatchDelete(entities), cancellationToken);
        }

        #endregion

        #region IEntityServiceAsync<T> 成员

        /// <summary>
        /// 异步根据条件更新多个字段的值。
        /// </summary>
        /// <param name="updateValues">要更新的字段及其值。</param>
        /// <param name="expr">更新条件。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>受影响的行数。</returns>
        public Task<int> UpdateValuesAsync(IEnumerable<KeyValuePair<string, object>> updateValues, Expr expr, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => UpdateValues(updateValues, expr), cancellationToken);
        }

        /// <summary>
        /// 异步根据主键更新多个字段的值。
        /// </summary>
        /// <param name="updateValues">要更新的字段及其值。</param>
        /// <param name="keys">主键值。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>是否更新成功。</returns>
        public Task<bool> UpdateValuesAsync(IEnumerable<KeyValuePair<string, object>> updateValues, object[] keys, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => UpdateValues(updateValues, keys), cancellationToken);
        }

        /// <summary>
        /// 异步根据 ID 删除实体。
        /// </summary>
        /// <param name="id">实体 ID。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>是否删除成功。</returns>
        public Task<bool> DeleteIDAsync(object id, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => DeleteID(id), cancellationToken);
        }

        #endregion
    }

    /// <summary>
    /// 当实体视图与实体类型相同时的实体服务基类。
    /// </summary>
    /// <typeparam name="T">实体类型。</typeparam>
    public class EntityService<T> : EntityService<T, T>
        where T : new()
    {
    }
}
