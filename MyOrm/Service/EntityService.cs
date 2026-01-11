using Microsoft.Extensions.DependencyInjection;
using MyOrm.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyOrm.Service
{
    public class EntityService<T, TView> : EntityViewService<TView>, IEntityService<T>, IEntityServiceAsync<T>, IEntityService, IEntityServiceAsync
    where TView : T, new()
    where T : new()
    {
        public IObjectDAO<T> ObjectDAO { get; set; }

        #region IEntityService<T> 成员

        public Type EntityType
        {
            get { return typeof(T); }
        }

        public virtual bool Insert(T entity)
        {
            return InsertCore(entity);
        }

        public virtual bool Update(T entity)
        {
            return UpdateCore(entity);
        }

        public virtual int UpdateValues(IEnumerable<KeyValuePair<string, object>> updateValues, Statement condition)
        {
            return ObjectDAO.UpdateAllValues(updateValues, condition);
        }

        public virtual bool UpdateValues(IEnumerable<KeyValuePair<string, object>> updateValues, params object[] keys)
        {
            return ObjectDAO.UpdateValues(updateValues, keys);
        }
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

        // Async variants for batch operations
        public virtual Task BatchAsync(IEnumerable<EntityOperation<T>> entities, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => Batch(entities), cancellationToken);
        }

        public virtual Task BatchInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => BatchInsert(entities), cancellationToken);
        }

        public virtual Task BatchUpdateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => BatchUpdate(entities), cancellationToken);
        }

        public virtual Task BatchUpdateOrInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => BatchUpdateOrInsert(entities), cancellationToken);
        }

        public virtual Task BatchDeleteAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => BatchDelete(entities), cancellationToken);
        }

        public virtual Task BatchDeleteIDAsync(IEnumerable ids, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => BatchDeleteID(ids), cancellationToken);
        }

        public virtual Task<bool> InsertAsync(T entity, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => Insert(entity), cancellationToken);
        }

        public virtual Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => Update(entity), cancellationToken);
        }

        public virtual Task<bool> UpdateOrInsertAsync(T entity, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => UpdateOrInsert(entity), cancellationToken);
        }

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

        public virtual bool DeleteID(object id)
        {
            return DeleteIDCore(id);
        }

        public virtual bool Delete(T entity)
        {
            return DeleteCore(entity);
        }

        public virtual int Delete(Statement condition)
        {
            return ObjectDAO.Delete(condition);
        }

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

        public virtual void BatchUpdate(IEnumerable<T> entities)
        {
            foreach (T entity in entities)
            {
                UpdateCore(entity);
            }
        }

        public virtual void BatchUpdateOrInsert(IEnumerable<T> entities)
        {
            foreach (T entity in entities)
            {
                UpdateOrInsert(entity);
            }
        }

        public virtual void BatchDelete(IEnumerable<T> entities)
        {
            foreach (T entity in entities)
            {
                DeleteCore(entity);
            }
        }

        public virtual void BatchDeleteID(IEnumerable ids)
        {
            foreach (object id in ids)
            {
                DeleteIDCore(id);
            }
        }

        #endregion

        #region NoNotify Methods

        protected virtual bool InsertCore(T entity)
        {
            if (entity is IArged arg)
                return ObjectDAO.WithArgs(arg.TableArgs).Insert(entity);
            return ObjectDAO.Insert(entity);
        }

        protected virtual bool UpdateCore(T entity)
        {
            if (entity is IArged arg)
                return ObjectDAO.WithArgs(arg.TableArgs).Update(entity);
            return ObjectDAO.Update(entity);
        }

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

        protected virtual bool DeleteIDCore(object id)
        {
            return ObjectDAO.DeleteByKeys(id);
        }

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
            if (entities is IEnumerable<T>)
                BatchInsert(entities as IEnumerable<T>);
            else
            {
                List<T> list = new List<T>();
                foreach (T entity in entities)
                {
                    list.Add(entity);
                }
                BatchInsert(list);
            }
        }

        void IEntityService.BatchUpdate(IEnumerable entities)
        {
            if (entities is IEnumerable<T>)
                BatchUpdate(entities as IEnumerable<T>);
            else
            {
                List<T> list = new List<T>();
                foreach (T entity in entities)
                {
                    list.Add(entity);
                }
                BatchUpdate(list);
            }
        }

        void IEntityService.BatchUpdateOrInsert(IEnumerable entities)
        {
            if (entities is IEnumerable<T>)
                BatchUpdateOrInsert(entities as IEnumerable<T>);
            else
            {
                List<T> list = new List<T>();
                foreach (T entity in entities)
                {
                    list.Add(entity);
                }
                BatchUpdateOrInsert(list);
            }
        }

        void IEntityService.BatchDelete(IEnumerable entities)
        {
            if (entities is IEnumerable<T>)
                BatchDelete(entities as IEnumerable<T>);
            else
            {
                List<T> list = new List<T>();
                foreach (T entity in entities)
                {
                    list.Add(entity);
                }
                BatchDelete(list);
            }
        }

        Task<bool> IEntityServiceAsync.InsertAsync(object entity, CancellationToken cancellationToken = default)
        {
            return InsertAsync((T)entity, cancellationToken);
        }

        Task<bool> IEntityServiceAsync.UpdateAsync(object entity, CancellationToken cancellationToken = default)
        {
            return UpdateAsync((T)entity, cancellationToken);
        }

        public Task<int> UpdateValuesAsync(IEnumerable<KeyValuePair<string, object>> updateValues, Statement condition, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => UpdateValues(updateValues, condition), cancellationToken);
        }

        public Task<bool> UpdateValuesAsync(IEnumerable<KeyValuePair<string, object>> updateValues, object[] keys, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => UpdateValues(updateValues, keys), cancellationToken);
        }

        Task<bool> IEntityServiceAsync.UpdateOrInsertAsync(object entity, CancellationToken cancellationToken = default)
        {
            return UpdateOrInsertAsync((T)entity, cancellationToken);
        }

        public Task<bool> DeleteIDAsync(object id, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => DeleteID(id), cancellationToken);
        }

        Task IEntityServiceAsync.BatchInsertAsync(IEnumerable entities, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => ((IEntityService)this).BatchInsert(entities), cancellationToken);
        }

        Task IEntityServiceAsync.BatchUpdateAsync(IEnumerable entities, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => ((IEntityService)this).BatchUpdate(entities), cancellationToken);
        }

        Task IEntityServiceAsync.BatchUpdateOrInsertAsync(IEnumerable entities, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => ((IEntityService)this).BatchUpdateOrInsert(entities), cancellationToken);
        }

        Task IEntityServiceAsync.BatchDeleteAsync(IEnumerable entities, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => ((IEntityService)this).BatchDelete(entities), cancellationToken);
        }

        #endregion
    }

    public class EntityService<T> : EntityService<T, T>
        where T : new()
    {
    }
}
