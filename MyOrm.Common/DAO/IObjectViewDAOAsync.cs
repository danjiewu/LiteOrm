using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace MyOrm.Common
{
    /// <summary>
    /// 异步版：实体视图查询操作的泛型接口
    /// </summary>
    /// <typeparam name="T">实体类类型</typeparam>
    public interface IObjectViewDAOAsync<T>
    {
        Task<T> GetObjectAsync(object[] keys, CancellationToken cancellationToken = default);

        Task<T> SearchOneAsync(Condition condition, CancellationToken cancellationToken = default);
        Task<T> SearchOneAsync(Expression<Func<T, bool>> expression, CancellationToken cancellationToken = default);

        Task ForEachAsync(Condition condition, Func<T, Task> func, CancellationToken cancellationToken = default);
        Task ForEachAsync(Expression<Func<T, bool>> expression, Func<T, Task> func, CancellationToken cancellationToken = default);

        Task<List<T>> SearchAsync(Condition condition = null, CancellationToken cancellationToken = default);
        Task<List<T>> SearchAsync(Condition condition, Sorting[] orderBy, CancellationToken cancellationToken = default);
        Task<List<T>> SearchAsync(Expression<Func<T, bool>> expression, CancellationToken cancellationToken = default);
        Task<List<T>> SearchAsync(Expression<Func<T, bool>> expression, Sorting[] orderBy, CancellationToken cancellationToken = default);

        Task<List<T>> SearchSectionAsync(Condition condition, SectionSet section, CancellationToken cancellationToken = default);
        Task<List<T>> SearchSectionAsync(Expression<Func<T, bool>> expression, SectionSet section, CancellationToken cancellationToken = default);

        Task<bool> ExistsKeyAsync(object[] keys, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(object o, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(Condition condition, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(Expression<Func<T, bool>> expression, CancellationToken cancellationToken = default);

        Task<int> CountAsync(Condition condition, CancellationToken cancellationToken = default);
        Task<int> CountAsync(Expression<Func<T, bool>> expression, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 异步版：实体视图查询操作的非泛型接口
    /// </summary>
    public interface IObjectViewDAOAsync
    {
        Task<object> GetObjectAsync(object[] keys, CancellationToken cancellationToken = default);

        Task<object> SearchOneAsync(Condition condition, CancellationToken cancellationToken = default);

        Task<IList> SearchAsync(Condition condition, CancellationToken cancellationToken = default);
        Task<IList> SearchAsync(Condition condition, Sorting[] orderBy, CancellationToken cancellationToken = default);

        Task<IList> SearchSectionAsync(Condition condition, SectionSet section, CancellationToken cancellationToken = default);

        Task<bool> ExistsKeyAsync(object[] keys, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(object o, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(Condition condition, CancellationToken cancellationToken = default);

        Task<int> CountAsync(Condition condition, CancellationToken cancellationToken = default);
    }
}