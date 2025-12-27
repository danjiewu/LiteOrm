using System;
using System.Collections.Generic;
using System.Collections;
using MyOrm.Common;
using System.ComponentModel;
using System.Security.Principal;
using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Runtime.InteropServices;

namespace MyOrm.Service
{
    [AutoRegister(ServiceLifetime.Singleton)]
    public abstract class ServiceBase
    {
        public ServiceBase()
        {
            Type serviceType = this.GetType();

            if (serviceType.IsGenericType)
            {
                int backtickIndex = serviceType.Name.IndexOf('`');
                ServiceName = serviceType.Name.Substring(0, backtickIndex) + "<" + String.Join(",", from t in serviceType.GetGenericArguments() select t.Name) + ">";
            }
            else
            {
                ServiceName = serviceType.Name;
            }
        }

        public virtual string ServiceName
        {
            get;
            set;
        }
    }

    public class EntityViewService<TView> : ServiceBase, IEntityViewService<TView>, IEntityViewService
         where TView : new()
    {
        protected virtual IObjectViewDAO<TView> ObjectViewDAO
        {
            get
            {
                return MyServiceProvider.Current.GetRequiredService<IObjectViewDAO<TView>>();
            }
        }


        #region IEntityViewService<T> 成员

        public Type ViewType
        {
            get { return typeof(TView); }
        }

        public virtual TView GetObject(object id, params string[] tableArgs)
        {
            return (tableArgs == null ? ObjectViewDAO : ObjectViewDAO.WithArgs(tableArgs)).GetObject(id);
        }

        public virtual bool ExistsID(object id, params string[] tableArgs)
        {
            return (tableArgs == null ? ObjectViewDAO : ObjectViewDAO.WithArgs(tableArgs)).Exists(new object[] { id });
        }

        public virtual bool Exists(Condition condition, params string[] tableArgs)
        {
            return (tableArgs == null ? ObjectViewDAO : ObjectViewDAO.WithArgs(tableArgs)).Exists(condition);
        }
        public virtual bool Exists(Expression<Func<TView, bool>> expression, params string[] tableArgs)
        {
            return (tableArgs == null ? ObjectViewDAO : ObjectViewDAO.WithArgs(tableArgs)).Exists(expression);
        }

        public virtual int Count(Condition condition = null, params string[] tableArgs)
        {
            return (tableArgs == null ? ObjectViewDAO : ObjectViewDAO.WithArgs(tableArgs)).Count(condition);
        }

        public virtual int Count(Expression<Func<TView, bool>> expression, params string[] tableArgs)
        {
            return (tableArgs == null ? ObjectViewDAO : ObjectViewDAO.WithArgs(tableArgs)).Count(expression);
        }

        public void ForEach(Condition condition, Action<TView> func, params string[] tableArgs)
        {
            (tableArgs == null ? ObjectViewDAO : ObjectViewDAO.WithArgs(tableArgs)).ForEach(condition, func);
        }

        public void ForEach(Expression<Func<TView, bool>> expression, Action<TView> func, params string[] tableArgs)
        {
            (tableArgs == null ? ObjectViewDAO : ObjectViewDAO.WithArgs(tableArgs)).ForEach(expression, func);
        }

        public virtual TView SearchOne(Condition condition, params string[] tableArgs)
        {
            return (tableArgs == null ? ObjectViewDAO : ObjectViewDAO.WithArgs(tableArgs)).SearchOne(condition);
        }

        public virtual TView SearchOne(Expression<Func<TView, bool>> expression, params string[] tableArgs)
        {
            return (tableArgs == null ? ObjectViewDAO : ObjectViewDAO.WithArgs(tableArgs)).SearchOne(expression);
        }

        public virtual List<TView> Search(Condition condition = null, params string[] tableArgs)
        {
            return (tableArgs == null ? ObjectViewDAO : ObjectViewDAO.WithArgs(tableArgs)).Search(condition);
        }

        public List<TView> Search(Expression<Func<TView, bool>> expression, params string[] tableArgs)
        {
            return (tableArgs == null ? ObjectViewDAO : ObjectViewDAO.WithArgs(tableArgs)).Search(expression);
        }
        public virtual List<TView> SearchWithOrder(Condition condition, Sorting[] orderBy = null, params string[] tableArgs)
        {
            return (tableArgs == null ? ObjectViewDAO : ObjectViewDAO.WithArgs(tableArgs)).Search(condition, orderBy);
        }

        public List<TView> SearchWithOrder(Expression<Func<TView, bool>> expression, Sorting[] orderBy = null, params string[] tableArgs)
        {
            return (tableArgs == null ? ObjectViewDAO : ObjectViewDAO.WithArgs(tableArgs)).Search(expression, orderBy);
        }

        public virtual List<TView> SearchSection(Condition condition, int startIndex, int sectionSize, Sorting[] orderBy = null, params string[] tableArgs)
        {
            SectionSet section = new SectionSet() { StartIndex = startIndex, SectionSize = sectionSize, Orders = orderBy };
            return (tableArgs == null ? ObjectViewDAO : ObjectViewDAO.WithArgs(tableArgs)).SearchSection(condition, section);
        }
        public List<TView> SearchSection(Expression<Func<TView, bool>> expression, int startIndex, int sectionSize, Sorting[] orderBy = null, params string[] tableArgs)
        {
            SectionSet section = new SectionSet() { StartIndex = startIndex, SectionSize = sectionSize, Orders = orderBy };
            return (tableArgs == null ? ObjectViewDAO : ObjectViewDAO.WithArgs(tableArgs)).SearchSection(expression, section);
        }

        public virtual List<TView> SearchSection(Condition condition, SectionSet section, params string[] tableArgs)
        {
            return (tableArgs == null ? ObjectViewDAO : ObjectViewDAO.WithArgs(tableArgs)).SearchSection(condition, section);
        }

        public List<TView> SearchSection(Expression<Func<TView, bool>> expression, SectionSet section, params string[] tableArgs)
        {
            return (tableArgs == null ? ObjectViewDAO : ObjectViewDAO.WithArgs(tableArgs)).SearchSection(expression, section);
        }

        #endregion

        #region IEntityViewService 成员

        object IEntityViewService.GetObject(object id, params string[] tableArgs)
        {
            return GetObject(id, tableArgs);
        }

        object IEntityViewService.SearchOne(Condition condition, params string[] tableArgs)
        {
            return SearchOne(condition, tableArgs);
        }

        IList IEntityViewService.Search(Condition condition, params string[] tableArgs)
        {
            return Search(condition, tableArgs);
        }

        IList IEntityViewService.SearchSection(Condition condition, int startIndex, int sectionSize, Sorting[] orderBy, params string[] tableArgs)
        {
            return SearchSection(condition, startIndex, sectionSize, orderBy, tableArgs);
        }

        IList IEntityViewService.SearchSection(Condition condition, SectionSet section, params string[] tableArgs)
        {
            return SearchSection(condition, section, tableArgs);
        }

        IList IEntityViewService.SearchWithOrder(Condition condition, Sorting[] orderBy, params string[] tableArgs)
        {
            return SearchWithOrder(condition, orderBy, tableArgs);
        }
        #endregion
    }

    public class EntityService<T, TView> : EntityViewService<TView>, IEntityService<T>, IEntityViewService<TView>, IEntityService, IEntityViewService
        where TView : T, new()
        where T : new()
    {
        protected virtual IObjectDAO<T> ObjectDAO
        {
            get
            {
                return MyServiceProvider.Current.GetRequiredService<IObjectDAO<T>>();
            }
        }

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

        public int UpdateValues(IEnumerable<KeyValuePair<string, object>> updateValues, Condition condition)
        {
            return ObjectDAO.UpdateValues(updateValues, condition);
        }

        public int UpdateValues(IEnumerable<KeyValuePair<string, object>> updateValues, Expression<Func<T, bool>> expression)
        {
            return ObjectDAO.UpdateValues(updateValues, expression);
        }

        public bool UpdateValues(IEnumerable<KeyValuePair<string, object>> updateValues, params object[] keys)
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
                        InsertCore(entityOp.Entity);
                        break;
                    case OpDef.Delete:
                        InsertCore(entityOp.Entity);
                        break;
                }
            }
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

        public virtual int Delete(Condition condition)
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

        public virtual void BatchDeleteID(IEnumerable<int> ids)
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
            return (entity is IArged ? ObjectDAO.WithArgs(((IArged)entity).TableArgs) : ObjectDAO).Insert(entity);
        }

        protected virtual bool UpdateCore(T entity)
        {
            return (entity is IArged ? ObjectDAO.WithArgs(((IArged)entity).TableArgs) : ObjectDAO).Update(entity);
        }

        protected virtual UpdateOrInsertResult UpdateOrInsertCore(T entity)
        {
            if ((entity is IArged ? ObjectViewDAO.WithArgs(((IArged)entity).TableArgs) : ObjectViewDAO).Exists(entity))
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
            return (entity is IArged ? ObjectDAO.WithArgs(((IArged)entity).TableArgs) : ObjectDAO).Delete(entity);
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

        #endregion



    }

    public class EntityService<T> : EntityService<T, T>
        where T : new()
    { }
}
