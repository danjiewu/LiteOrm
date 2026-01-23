using Microsoft.Extensions.DependencyInjection;
using LiteOrm.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Service
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
    [AutoRegister(ServiceLifetime.Scoped)]
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
        /// 根据条件更新多个字段的值。
        /// </summary>
        /// <remarks>
        /// 此方法允许通过 Lambda 表达式条件批量更新满足条件的所有记录的指定字段。
        /// </remarks>
        /// <param name="updateValues">要更新的字段及其值。键为字段名，值为新的字段值。</param>
        /// <param name="expr">更新条件表达式，用于筛选要更新的记录。</param>
        /// <param name="tableArgs">表名参数，用于支持分表场景。</param>
        /// <returns>更新的记录数。</returns>
        public virtual int UpdateValues(IEnumerable<KeyValuePair<string, object>> updateValues, Expr expr, params string[] tableArgs)
        {
            return ObjectDAO.WithArgs(tableArgs).UpdateAllValues(updateValues, expr);
        }

        /// <summary>
        /// 根据主键更新多个字段的值。
        /// </summary>
        /// <remarks>
        /// 此方法根据实体的主键值快速更新指定字段，性能优于基于整个实体的更新操作。
        /// </remarks>
        /// <param name="updateValues">要更新的字段及其值。键为字段名，值为新的字段值。</param>
        /// <param name="keys">主键值数组，对应实体的主键列。</param>
        /// <param name="tableArgs">表名参数，用于支持分表场景。</param>
        /// <returns>是否更新成功。</returns>
        public virtual bool UpdateValues(IEnumerable<KeyValuePair<string, object>> updateValues, object[] keys, params string[] tableArgs)
        {
            return ObjectDAO.WithArgs(tableArgs).UpdateValues(updateValues, keys);
        }

        /// <summary>
        /// 批量处理实体操作（插入、更新、删除）。
        /// </summary>
        /// <remarks>
        /// 此方法接收一个 EntityOperation 集合，根据每个操作的类型执行相应的数据库操作。
        /// 支持在单个批处理中混合执行插入、更新、删除操作。
        /// </remarks>
        /// <param name="entities">实体操作集合，每个项目包含实体和操作类型（插入、更新或删除）。</param>
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
        /// 异步批量处理实体操作（插入、更新、删除）。
        /// </summary>
        /// <remarks>
        /// 该方法在会话上下文中异步执行批量操作，支持事务处理和异步编程模型。
        /// </remarks>
        /// <param name="entities">实体操作集合。</param>
        /// <param name="cancellationToken">取消令牌，用于支持异步操作的取消。</param>
        /// <returns>表示异步操作的任务。</returns>
        public async virtual Task BatchAsync(IEnumerable<EntityOperation<T>> entities, CancellationToken cancellationToken = default)
        {
            foreach (EntityOperation<T> entityOp in entities)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (entityOp.Operation)
                {
                    case OpDef.Insert:
                        await InsertCoreAsync(entityOp.Entity, cancellationToken);
                        break;
                    case OpDef.Update:
                        await UpdateCoreAsync(entityOp.Entity, cancellationToken);
                        break;
                    case OpDef.Delete:
                        await DeleteCoreAsync(entityOp.Entity, cancellationToken);
                        break;
                }
            }
        }

        /// <summary>
        /// 异步批量插入实体。
        /// </summary>
        /// <remarks>
        /// 该方法在会话上下文中异步执行批量插入操作，自动处理分表（分片）场景。
        /// </remarks>
        /// <param name="entities">要插入的实体集合。</param>
        /// <param name="cancellationToken">取消令牌，用于支持异步操作的取消。</param>
        /// <returns>表示异步操作的任务。</returns>
        public async virtual Task BatchInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            if (typeof(IArged).IsAssignableFrom(typeof(T)))
            {
                var groups = entities.ToLookup(t => ((IArged)t).TableArgs, StringArrayEqualityComparer.Instance);
                foreach (var group in groups)
                {
                    await ObjectDAO.WithArgs(group.Key).BatchInsertAsync(group, cancellationToken);
                }
            }
            else
                await ObjectDAO.BatchInsertAsync(entities, cancellationToken);
        }

        /// <summary>
        /// 异步批量更新实体。
        /// </summary>
        /// <remarks>
        /// 该方法在会话上下文中异步执行批量更新操作，逐个更新集合中的每个实体。
        /// </remarks>
        /// <param name="entities">要更新的实体集合。</param>
        /// <param name="cancellationToken">取消令牌，用于支持异步操作的取消。</param>
        /// <returns>表示异步操作的任务。</returns>
        public async virtual Task BatchUpdateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            foreach (T entity in entities)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UpdateCoreAsync(entity, cancellationToken);
            }
        }

        /// <summary>
        /// 异步批量更新或插入实体。
        /// </summary>
        /// <remarks>
        /// 该方法在会话上下文中异步执行批量更新或插入操作。对于集合中的每个实体，
        /// 如果主键已存在则更新，否则插入新记录。
        /// </remarks>
        /// <param name="entities">要处理的实体集合。</param>
        /// <param name="cancellationToken">取消令牌，用于支持异步操作的取消。</param>
        /// <returns>表示异步操作的任务。</returns>
        public async virtual Task BatchUpdateOrInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            foreach (T entity in entities)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UpdateOrInsertAsync(entity, cancellationToken);
            }
        }

        /// <summary>
        /// 异步批量删除实体。
        /// </summary>
        /// <remarks>
        /// 该方法在会话上下文中异步执行批量删除操作，逐个删除集合中的每个实体。
        /// 实体必须包含有效的主键值。
        /// </remarks>
        /// <param name="entities">要删除的实体集合。</param>
        /// <param name="cancellationToken">取消令牌，用于支持异步操作的取消。</param>
        /// <returns>表示异步操作的任务。</returns>
        public async virtual Task BatchDeleteAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            foreach (T entity in entities)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await DeleteCoreAsync(entity, cancellationToken);
            }
        }

        /// <summary>
        /// 异步批量根据 ID 删除实体。
        /// </summary>
        /// <remarks>
        /// 该方法在会话上下文中异步执行批量删除操作，根据 ID 集合快速删除相应的记录。
        /// 适用于需要根据主键快速删除多条记录的场景。
        /// </remarks>
        /// <param name="ids">要删除的实体 ID 集合。</param>
        /// <param name="cancellationToken">取消令牌，用于支持异步操作的取消。</param>
        /// <returns>表示异步操作的任务。</returns>
        public async virtual Task BatchDeleteIDAsync(IEnumerable ids, CancellationToken cancellationToken = default)
        {
            foreach (object id in ids)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await DeleteIDCoreAsync(id, null, cancellationToken);
            }
        }

        /// <summary>
        /// 异步插入实体。
        /// </summary>
        /// <remarks>
        /// 该方法在会话上下文中异步执行插入操作，返回插入是否成功的布尔值。
        /// 如果实体实现了 IArged 接口，会自动使用其分表参数。
        /// </remarks>
        /// <param name="entity">要插入的实体。</param>
        /// <param name="cancellationToken">取消令牌，用于支持异步操作的取消。</param>
        /// <returns>表示异步操作的任务，任务结果为是否插入成功。</returns>
        public async virtual Task<bool> InsertAsync(T entity, CancellationToken cancellationToken = default)
        {
            return await InsertCoreAsync(entity, cancellationToken);
        }

        /// <summary>
        /// 异步更新实体。
        /// </summary>
        /// <remarks>
        /// 该方法在会话上下文中异步执行更新操作，根据实体的主键值更新对应的记录。
        /// 如果实体实现了 IArged 接口，会自动使用其分表参数。
        /// </remarks>
        /// <param name="entity">要更新的实体。</param>
        /// <param name="cancellationToken">取消令牌，用于支持异步操作的取消。</param>
        /// <returns>表示异步操作的任务，任务结果为是否更新成功。</returns>
        public async virtual Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            return await UpdateCoreAsync(entity, cancellationToken);
        }

        /// <summary>
        /// 异步更新或插入实体。
        /// </summary> 
        /// <remarks>
        /// 该方法在会话上下文中异步执行更新或插入操作，根据实体的主键值决定是更新还是插入。
        /// </remarks>
        /// <param name="entity">要更新或插入的实体。</param>
        /// <param name="cancellationToken">取消令牌，用于支持异步操作的取消。</param>
        /// <returns>表示异步操作的任务，任务结果为是否操作成功。</returns>
        public async virtual Task<bool> UpdateOrInsertAsync(T entity, CancellationToken cancellationToken = default)
        {
            switch (await UpdateOrInsertCoreAsync(entity, cancellationToken))
            {
                case UpdateOrInsertResult.Inserted:
                case UpdateOrInsertResult.Updated:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 更新或插入实体。
        /// </summary>
        /// <remarks>
        /// 根据实体的主键检查记录是否存在：
        /// 如果主键已存在则执行更新操作，否则执行插入操作。
        /// 这是一个原子操作，适用于"upsert"（更新或插入）场景。
        /// </remarks>
        /// <param name="entity">要更新或插入的实体。</param>
        /// <returns>是否操作成功（插入或更新）。</returns>
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
        /// 根据 ID 删除实体。
        /// </summary>
        /// <remarks>
        /// 根据实体的主键值快速删除相应的记录。这是按主键删除的最常用方法。
        /// </remarks>
        /// <param name="id">实体的主键值。</param>
        /// <param name="tableArgs">表名参数，用于支持分表场景。</param>
        /// <returns>是否删除成功。</returns>
        public virtual bool DeleteID(object id, params string[] tableArgs)
        {
            return DeleteIDCore(id, tableArgs);
        }

        /// <summary>
        /// 删除实体。
        /// </summary>
        /// <remarks>
        /// 根据实体的主键值删除相应的数据库记录。实体必须包含有效的主键值。
        /// 如果实体实现了 IArged 接口，会自动使用其分表参数。
        /// </remarks>
        /// <param name="entity">要删除的实体</param>
        /// <returns>是否删除成功</returns>
        public virtual bool Delete(T entity)
        {
            return DeleteCore(entity);
        }

        /// <summary>
        /// 根据条件删除实体。
        /// </summary>
        /// <remarks>
        /// 根据 Lambda 表达式条件批量删除满足条件的所有记录。
        /// 支持复杂的条件表达式来精确控制删除范围。
        /// </remarks>
        /// <param name="expr">删除条件表达式，用于筛选要删除的记录。</param>
        /// <param name="tableArgs">表名参数，用于支持分表场景。</param>
        /// <returns>删除的记录数。</returns>
        public virtual int Delete(Expr expr, params string[] tableArgs)
        {
            return ObjectDAO.WithArgs(tableArgs).Delete(expr);
        }

        /// <summary>
        /// 批量插入实体。
        /// </summary>
        /// <remarks>
        /// 此方法将集合中的所有实体批量插入到数据库。
        /// 如果实体实现了 IArged 接口，会自动按分表参数分组，分别对不同的分表执行插入操作。
        /// </remarks>
        /// <param name="entities">要插入的实体集合。</param>
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
        /// 批量更新实体。
        /// </summary>
        /// <remarks>
        /// 此方法逐个更新集合中的每个实体。每个实体更新都基于其主键值。
        /// 如果实体实现了 IArged 接口，会自动使用其分表参数。
        /// </remarks>
        /// <param name="entities">要更新的实体集合。</param>
        public virtual void BatchUpdate(IEnumerable<T> entities)
        {
            foreach (T entity in entities)
            {
                UpdateCore(entity);
            }
        }

        /// <summary>
        /// 批量更新或插入实体。
        /// </summary>
        /// <remarks>
        /// 此方法对集合中的每个实体执行更新或插入操作。对于每个实体：
        /// 如果主键已存在则执行更新，否则执行插入操作。
        /// </remarks>
        /// <param name="entities">要处理的实体集合。</param>
        public virtual void BatchUpdateOrInsert(IEnumerable<T> entities)
        {
            foreach (T entity in entities)
            {
                UpdateOrInsert(entity);
            }
        }

        /// <summary>
        /// 批量删除实体。
        /// </summary>
        /// <remarks>
        /// 此方法根据实体集合中每个实体的主键值逐个删除它们。
        /// 实体必须包含有效的主键值。
        /// 如果实体实现了 IArged 接口，会自动使用其分表参数。
        /// </remarks>
        /// <param name="entities">要删除的实体集合。</param>
        public virtual void BatchDelete(IEnumerable<T> entities)
        {
            foreach (T entity in entities)
            {
                DeleteCore(entity);
            }
        }

        /// <summary>
        /// 批量根据 ID 删除实体。
        /// </summary>
        /// <remarks>
        /// 此方法根据提供的 ID 集合逐个删除相应的记录。
        /// 这是一个高效的按主键删除多条记录的方法。
        /// </remarks>
        /// <param name="ids">要删除的实体 ID 集合。</param>
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
        /// <remarks>
        /// 这是内部使用的核心插入方法，处理 IArged 接口的分表参数。
        /// 不触发任何通知或验证，直接执行数据库插入操作。
        /// </remarks>
        /// <param name="entity">要插入的实体对象。</param>
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
        /// <remarks>
        /// 这是内部使用的核心更新方法，处理 IArged 接口的分表参数。
        /// 根据实体的主键值更新对应的数据库记录。
        /// 不触发任何通知或验证，直接执行数据库更新操作。
        /// </remarks>
        /// <param name="entity">要更新的实体对象。</param>
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
        /// <remarks>
        /// 这是内部使用的核心更新或插入方法。首先检查实体是否已存在：
        /// 通过查询相同主键的记录来判断。如果存在则执行更新，否则执行插入。
        /// 处理 IArged 接口的分表参数，支持分表场景。
        /// </remarks>
        /// <param name="entity">要处理的实体对象。</param>
        /// <returns>操作结果（已插入、已更新或失败）。</returns>
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
        /// <remarks>
        /// 这是内部使用的核心按主键删除方法。
        /// 根据提供的主键值删除对应的数据库记录。
        /// 不触发任何通知或验证，直接执行数据库删除操作。
        /// </remarks>
        /// <param name="id">实体的主键值。</param>
        /// <param name="tableArgs">表名参数，用于支持分表场景。</param>
        /// <returns>是否删除成功。</returns>
        protected virtual bool DeleteIDCore(object id, params string[] tableArgs)
        {
            return ObjectDAO.WithArgs(tableArgs).DeleteByKeys(id);
        }

        /// <summary>
        /// 核心删除逻辑。
        /// </summary>
        /// <remarks>
        /// 这是内部使用的核心删除方法，处理 IArged 接口的分表参数。
        /// 根据实体的主键值删除对应的数据库记录。
        /// 不触发任何通知或验证，直接执行数据库删除操作。
        /// </remarks>
        /// <param name="entity">要删除的实体对象。</param>
        /// <returns>是否删除成功。</returns>
        protected virtual bool DeleteCore(T entity)
        {
            if (entity is IArged arg)
                return ObjectDAO.WithArgs(arg.TableArgs).Delete(entity);
            return ObjectDAO.Delete(entity);
        }
        #endregion

        #region IEntityService 成员

        /// <summary>
        /// 隐式接口实现：插入实体对象的非泛型版本。
        /// </summary>
        /// <param name="entity">要插入的实体对象。</param>
        /// <returns>是否插入成功。</returns>
        bool IEntityService.Insert(object entity)
        {
            return Insert((T)entity);
        }

        /// <summary>
        /// 隐式接口实现：更新实体对象的非泛型版本。
        /// </summary>
        /// <param name="entity">要更新的实体对象。</param>
        /// <returns>是否更新成功。</returns>
        bool IEntityService.Update(object entity)
        {
            return Update((T)entity);
        }

        /// <summary>
        /// 隐式接口实现：根据条件删除实体的非泛型版本。
        /// </summary>
        /// <param name="expr">删除条件表达式。</param>
        /// <param name="tableArgs">表名参数。</param>
        /// <returns>删除的记录数。</returns>
        int IEntityService.Delete(Expr expr, params string[] tableArgs)
        {
            return Delete(expr, tableArgs);
        }

        /// <summary>
        /// 隐式接口实现：更新或插入实体对象的非泛型版本。
        /// </summary>
        /// <param name="entity">要处理的实体对象。</param>
        /// <returns>是否操作成功。</returns>
        bool IEntityService.UpdateOrInsert(object entity)
        {
            return UpdateOrInsert((T)entity);
        }

        /// <summary>
        /// 隐式接口实现：批量插入实体集合的非泛型版本。
        /// </summary>
        /// <param name="entities">实体集合。</param>
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

        /// <summary>
        /// 隐式接口实现：批量更新实体集合的非泛型版本。
        /// </summary>
        /// <param name="entities">实体集合。</param>
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

        /// <summary>
        /// 隐式接口实现：批量更新或插入实体集合的非泛型版本。
        /// </summary>
        /// <param name="entities">实体集合。</param>
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

        /// <summary>
        /// 隐式接口实现：批量删除实体集合的非泛型版本。
        /// </summary>
        /// <param name="entities">实体集合。</param>
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

        /// <summary>
        /// 隐式接口实现：异步插入实体对象的非泛型版本。
        /// </summary>
        /// <param name="entity">要插入的实体对象。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，任务结果为是否插入成功。</returns>
        async Task<bool> IEntityServiceAsync.InsertAsync(object entity, CancellationToken cancellationToken)
        {
            return await InsertAsync((T)entity, cancellationToken);
        }

        /// <summary>
        /// 隐式接口实现：异步更新实体对象的非泛型版本。
        /// </summary>
        /// <param name="entity">要更新的实体对象。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，任务结果为是否更新成功。</returns>
        async Task<bool> IEntityServiceAsync.UpdateAsync(object entity, CancellationToken cancellationToken)
        {
            return await UpdateAsync((T)entity, cancellationToken);
        }

        /// <summary>
        /// 隐式接口实现：异步根据条件删除实体的非泛型版本。
        /// </summary>
        /// <param name="expr">删除条件表达式。</param>
        /// <param name="tableArgs">表名参数。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，任务结果为删除的记录数。</returns>
        async Task<int> IEntityServiceAsync.DeleteAsync(Expr expr, string[] tableArgs, CancellationToken cancellationToken)
        {
            return await DeleteAsync(expr, tableArgs, cancellationToken);
        }

        /// <summary>
        /// 隐式接口实现：异步更新或插入实体对象的非泛型版本。
        /// </summary>
        /// <param name="entity">要处理的实体对象。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，任务结果为是否操作成功。</returns>
        async Task<bool> IEntityServiceAsync.UpdateOrInsertAsync(object entity, CancellationToken cancellationToken)
        {
            return await UpdateOrInsertAsync((T)entity, cancellationToken);
        }

        /// <summary>
        /// 隐式接口实现：异步批量插入实体集合的非泛型版本。
        /// </summary>
        /// <param name="entities">实体集合。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务。</returns>
        async Task IEntityServiceAsync.BatchInsertAsync(IEnumerable entities, CancellationToken cancellationToken)
        {
            if (entities is IEnumerable<T> typed)
                await BatchInsertAsync(typed, cancellationToken);
            else
            {
                var list = new List<T>();
                foreach (object entity in entities)
                {
                    list.Add((T)entity);
                }
                await BatchInsertAsync(list, cancellationToken);
            }
        }

        /// <summary>
        /// 隐式接口实现：异步批量更新实体集合的非泛型版本。
        /// </summary>
        /// <param name="entities">实体集合。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务。</returns>
        async Task IEntityServiceAsync.BatchUpdateAsync(IEnumerable entities, CancellationToken cancellationToken)
        {
            if (entities is IEnumerable<T> typed)
                await BatchUpdateAsync(typed, cancellationToken);
            else
            {
                var list = new List<T>();
                foreach (object entity in entities)
                {
                    list.Add((T)entity);
                }
                await BatchUpdateAsync(list, cancellationToken);
            }
        }

        /// <summary>
        /// 隐式接口实现：异步批量更新或插入实体集合的非泛型版本。
        /// </summary>
        /// <param name="entities">实体集合。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务。</returns>
        async Task IEntityServiceAsync.BatchUpdateOrInsertAsync(IEnumerable entities, CancellationToken cancellationToken)
        {
            if (entities is IEnumerable<T> typed)
                await BatchUpdateOrInsertAsync(typed, cancellationToken);
            else
            {
                var list = new List<T>();
                foreach (object entity in entities)
                {
                    list.Add((T)entity);
                }
                await BatchUpdateOrInsertAsync(list, cancellationToken);
            }
        }

        /// <summary>
        /// 隐式接口实现：异步批量删除实体集合的非泛型版本。
        /// </summary>
        /// <param name="entities">实体集合。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务。</returns>
        async Task IEntityServiceAsync.BatchDeleteAsync(IEnumerable entities, CancellationToken cancellationToken)
        {
            if (entities is IEnumerable<T> typed)
                await BatchDeleteAsync(typed, cancellationToken);
            else
            {
                var list = new List<T>();
                foreach (object entity in entities)
                {
                    list.Add((T)entity);
                }
                await BatchDeleteAsync(list, cancellationToken);
            }
        }

        #endregion

        #region IEntityServiceAsync<T> 成员

        /// <summary>
        /// 异步根据条件更新多个字段的值。
        /// </summary>
        /// <param name="updateValues">要更新的字段及其值。</param>
        /// <param name="expr">更新条件。</param>
        /// <param name="tableArgs">表名参数。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>受影响的行数。</returns>
        public async Task<int> UpdateValuesAsync(IEnumerable<KeyValuePair<string, object>> updateValues, Expr expr, string[] tableArgs, CancellationToken cancellationToken = default)
        {
            return await ObjectDAO.WithArgs(tableArgs).UpdateAllValuesAsync(updateValues, expr, cancellationToken);
        }

        /// <summary>
        /// 异步根据条件删除实体。
        /// </summary>
        /// <param name="expr">删除条件。</param>
        /// <param name="tableArgs">表名参数。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>受影响的行数。</returns>
        public async Task<int> DeleteAsync(Expr expr, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return await ObjectDAO.WithArgs(tableArgs).DeleteAsync(expr, cancellationToken);
        }

        /// <summary>
        /// 异步根据主键更新多个字段的值。
        /// </summary>
        /// <param name="updateValues">要更新的字段及其值。</param>
        /// <param name="keys">主键值。</param>
        /// <param name="tableArgs">表名参数。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>是否更新成功。</returns>
        public async Task<bool> UpdateValuesAsync(IEnumerable<KeyValuePair<string, object>> updateValues, object[] keys, string[] tableArgs, CancellationToken cancellationToken = default)
        {            
            return await ObjectDAO.WithArgs(tableArgs).UpdateValuesAsync(updateValues, keys, cancellationToken);
        }

        /// <summary>
        /// 异步根据 ID 删除实体。
        /// </summary>
        /// <param name="id">实体 ID。</param>
        /// <param name="tableArgs">表名参数。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>是否删除成功。</returns>
        public async Task<bool> DeleteIDAsync(object id, string[] tableArgs, CancellationToken cancellationToken = default)
        {
            return await DeleteIDCoreAsync(id, tableArgs, cancellationToken);
        }

        /// <summary>
        /// 异步删除实体。
        /// </summary>
        /// <param name="entity">要删除的实体。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，任务结果为是否删除成功。</returns>
        public async virtual Task<bool> DeleteAsync(T entity, CancellationToken cancellationToken = default)
        {
            return await DeleteCoreAsync(entity, cancellationToken);
        }

        #endregion

        #region Core Async Methods

        /// <summary>
        /// 核心异步插入逻辑。
        /// </summary>
        /// <param name="entity">要插入的实体对象。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>是否插入成功。</returns>
        protected virtual async Task<bool> InsertCoreAsync(T entity, CancellationToken cancellationToken = default)
        {
            if (entity is IArged arg)
                return await ObjectDAO.WithArgs(arg.TableArgs).InsertAsync(entity, cancellationToken);
            return await ObjectDAO.InsertAsync(entity, cancellationToken);
        }

        /// <summary>
        /// 核心异步更新逻辑。
        /// </summary>
        /// <param name="entity">要更新的实体对象。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>是否更新成功。</returns>
        protected virtual async Task<bool> UpdateCoreAsync(T entity, CancellationToken cancellationToken = default)
        {
            if (entity is IArged arg)
                return await ObjectDAO.WithArgs(arg.TableArgs).UpdateAsync(entity, null, cancellationToken);
            return await ObjectDAO.UpdateAsync(entity, null, cancellationToken);
        }

        /// <summary>
        /// 核心异步更新或插入逻辑。
        /// </summary>
        /// <param name="entity">要处理的实体对象。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>操作结果。</returns>
        protected virtual async Task<UpdateOrInsertResult> UpdateOrInsertCoreAsync(T entity, CancellationToken cancellationToken = default)
        {
            bool exists;
            if (entity is IArged arg)
            {
                exists = await ObjectViewDAO.WithArgs(arg.TableArgs).ExistsAsync(entity, cancellationToken);
            }
            else
            {
                exists = await ObjectViewDAO.ExistsAsync(entity, cancellationToken);
            }

            if (exists)
                return await UpdateCoreAsync(entity, cancellationToken) ? UpdateOrInsertResult.Updated : UpdateOrInsertResult.Failed;
            else
                return await InsertCoreAsync(entity, cancellationToken) ? UpdateOrInsertResult.Inserted : UpdateOrInsertResult.Failed;
        }

        /// <summary>
        /// 核心异步基于 ID 的删除逻辑。
        /// </summary>
        /// <param name="id">实体的主键值。</param>
        /// <param name="tableArgs">表名参数。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>是否删除成功。</returns>
        protected virtual async Task<bool> DeleteIDCoreAsync(object id, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return await ObjectDAO.WithArgs(tableArgs).DeleteByKeysAsync(new object[] { id }, cancellationToken);
        }

        /// <summary>
        /// 核心异步删除逻辑。
        /// </summary>
        /// <param name="entity">要删除的实体对象。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>是否删除成功。</returns>
        protected virtual async Task<bool> DeleteCoreAsync(T entity, CancellationToken cancellationToken = default)
        {
            if (entity is IArged arg)
                return await ObjectDAO.WithArgs(arg.TableArgs).DeleteAsync(entity, cancellationToken);
            return await ObjectDAO.DeleteAsync(entity, cancellationToken);
        }

        #endregion
    }

    /// <summary>
    /// 当实体视图与实体类型相同时的实体服务基类。
    /// </summary>
    /// <remarks>
    /// 这个泛型类是 EntityService&lt;T, TView&gt; 的便利版本，当实体和实体视图类型相同时使用。
    /// 它简化了在这种常见场景中的类型参数传递。
    /// </remarks>
    /// <typeparam name="T">实体类型，同时也是实体视图类型。</typeparam>
    [AutoRegister(ServiceLifetime.Scoped)]
    public class EntityService<T> : EntityService<T, T>
        where T : new()
    {
    }
}
