using Autofac.Extras.DynamicProxy;
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
    /// <summary>
    /// 提供视图实体的通用服务实现，支持同步与异步操作。
    /// </summary>
    [AutoRegister(ServiceLifetime.Singleton)]
    [Intercept(typeof(ServiceInvokeInterceptor))]
    public class EntityViewService<TView> : IEntityViewService<TView>, IEntityViewServiceAsync<TView>, IEntityViewService, IEntityViewServiceAsync
         where TView : new()
    {
        public IObjectViewDAO<TView> ObjectViewDAO { get; set; }

        #region IEntityViewService<T> 成员

        public Type ViewType
        {
            get { return typeof(TView); }
        }

        public virtual TView GetObject(object id, params string[] tableArgs)
        {
            return ObjectViewDAO.WithArgs(tableArgs).GetObject(id);
        }

        public virtual bool ExistsID(object id, params string[] tableArgs)
        {
            return ObjectViewDAO.WithArgs(tableArgs).Exists(new object[] { id });
        }

        public virtual bool Exists(Statement condition, params string[] tableArgs)
        {
            return ObjectViewDAO.WithArgs(tableArgs).Exists(condition);
        }
        public virtual bool Exists(Expression<Func<TView, bool>> expression, params string[] tableArgs)
        {
            return ObjectViewDAO.WithArgs(tableArgs).Exists(expression);
        }

        public virtual int Count(Statement condition = null, params string[] tableArgs)
        {
            return ObjectViewDAO.WithArgs(tableArgs).Count(condition);
        }


        public virtual void ForEach(Statement condition, Action<TView> func, params string[] tableArgs)
        {
            ObjectViewDAO.WithArgs(tableArgs).ForEach(condition, func);
        }


        public virtual TView SearchOne(Statement condition, params string[] tableArgs)
        {
            return ObjectViewDAO.WithArgs(tableArgs).SearchOne(condition);
        }

        public virtual List<TView> Search(Statement condition = null, params string[] tableArgs)
        {
            return ObjectViewDAO.WithArgs(tableArgs).Search(condition);
        }

        public virtual List<TView> SearchWithOrder(Statement condition, Sorting[] orderBy = null, params string[] tableArgs)
        {
            return ObjectViewDAO.WithArgs(tableArgs).Search(condition, orderBy);
        }

        public virtual List<TView> SearchSection(Statement condition, int startIndex, int sectionSize, Sorting[] orderBy = null, params string[] tableArgs)
        {
            SectionSet section = new SectionSet(startIndex, sectionSize);
            section.Orders.AddRange(orderBy ?? Array.Empty<Sorting>());
            return ObjectViewDAO.WithArgs(tableArgs).SearchSection(condition, section);
        }

        public virtual List<TView> SearchSection(Statement condition, SectionSet section, params string[] tableArgs)
        {
            return ObjectViewDAO.WithArgs(tableArgs).SearchSection(condition, section);
        }

        #endregion

        #region IEntityViewService 成员

        object IEntityViewService.GetObject(object id, params string[] tableArgs)
        {
            return GetObject(id, tableArgs);
        }

        object IEntityViewService.SearchOne(Statement condition, params string[] tableArgs)
        {
            return SearchOne(condition, tableArgs);
        }

        IList IEntityViewService.Search(Statement condition, params string[] tableArgs)
        {
            return Search(condition, tableArgs);
        }

        IList IEntityViewService.SearchSection(Statement condition, int startIndex, int sectionSize, Sorting[] orderBy, params string[] tableArgs)
        {
            return SearchSection(condition, startIndex, sectionSize, orderBy, tableArgs);
        }

        IList IEntityViewService.SearchSection(Statement condition, SectionSet section, params string[] tableArgs)
        {
            return SearchSection(condition, section, tableArgs);
        }

        IList IEntityViewService.SearchWithOrder(Statement condition, Sorting[] orderBy, params string[] tableArgs)
        {
            return SearchWithOrder(condition, orderBy, tableArgs);
        }
        #endregion

        #region IEntityViewServiceAsync 实现

        Task<object> IEntityViewServiceAsync.GetObjectAsync(object id, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => (object)GetObject(id, tableArgs), cancellationToken);
        }

        public virtual Task<bool> ExistsIDAsync(object id, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => ExistsID(id, tableArgs), cancellationToken);
        }

        public virtual Task<bool> ExistsAsync(Statement condition, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => Exists(condition, tableArgs), cancellationToken);
        }

        public virtual Task<int> CountAsync(Statement condition = null, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => Count(condition, tableArgs), cancellationToken);
        }

        Task<object> IEntityViewServiceAsync.SearchOneAsync(Statement condition, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => (object)SearchOne(condition, tableArgs), cancellationToken);
        }

        Task<IList> IEntityViewServiceAsync.SearchAsync(Statement condition = null, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => (IList)Search(condition, tableArgs), cancellationToken);
        }

        Task<IList> IEntityViewServiceAsync.SearchWithOrderAsync(Statement condition, Sorting[] orderBy = null, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => (IList)SearchWithOrder(condition, orderBy, tableArgs), cancellationToken);
        }

        Task<IList> IEntityViewServiceAsync.SearchSectionAsync(Statement condition, int startIndex, int sectionSize, Sorting[] orderBy = null, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => (IList)SearchSection(condition, startIndex, sectionSize, orderBy, tableArgs), cancellationToken);
        }

        Task<IList> IEntityViewServiceAsync.SearchSectionAsync(Statement condition, SectionSet section, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => (IList)SearchSection(condition, section, tableArgs), cancellationToken);
        }

        public virtual Task<TView> GetObjectAsync(object id, string[] tableArgs, CancellationToken cancellationToken)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => GetObject(id, tableArgs), cancellationToken);
        }

        public virtual Task<TView> SearchOneAsync(Statement condition, string[] tableArgs, CancellationToken cancellationToken)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => SearchOne(condition, tableArgs), cancellationToken);
        }

        public virtual Task<bool> ExistsAsync(Expression<Func<TView, bool>> expression, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => Exists(expression, tableArgs), cancellationToken);
        }


        public virtual Task ForEachAsync(Statement condition, Func<TView, Task> func, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return SessionManager.Current.ExecuteInSessionAsync(async () =>
            {
                await ObjectViewDAO.WithArgs(tableArgs).ForEachAsync(condition, func, cancellationToken);
            }, cancellationToken);
        }

        public virtual Task<List<TView>> SearchAsync(Statement condition, string[] tableArgs, CancellationToken cancellationToken)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => Search(condition, tableArgs), cancellationToken);
        }

        public virtual Task<List<TView>> SearchWithOrderAsync(Statement condition, Sorting[] orderBy, string[] tableArgs, CancellationToken cancellationToken)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => SearchWithOrder(condition, orderBy, tableArgs), cancellationToken);
        }

        public virtual Task<List<TView>> SearchSectionAsync(Statement condition, int startIndex, int sectionSize, Sorting[] orderBy, string[] tableArgs, CancellationToken cancellationToken)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => SearchSection(condition, startIndex, sectionSize, orderBy, tableArgs), cancellationToken);
        }

        public virtual Task<List<TView>> SearchSectionAsync(Statement condition, SectionSet section, string[] tableArgs, CancellationToken cancellationToken)
        {
            return SessionManager.Current.ExecuteInSessionAsync(() => SearchSection(condition, section, tableArgs), cancellationToken);
        }
        #endregion
    }
}
