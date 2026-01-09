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

        Task<T> SearchOneAsync(Statement condition, CancellationToken cancellationToken = default);

        Task ForEachAsync(Statement condition, Func<T, Task> func, CancellationToken cancellationToken = default);

        Task<List<T>> SearchAsync(Statement condition = null, CancellationToken cancellationToken = default);
        Task<List<T>> SearchSectionAsync(Statement condition, SectionSet section, CancellationToken cancellationToken = default);
        Task<bool> ExistsKeyAsync(object[] keys, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(object o, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(Statement condition, CancellationToken cancellationToken = default);
        Task<int> CountAsync(Statement condition, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 异步版：实体视图查询操作的非泛型接口
    /// </summary>
    [AutoRegister(false)]
    public interface IObjectViewDAOAsync
    {
        Task<object> GetObjectAsync(object[] keys, CancellationToken cancellationToken = default);

        Task<object> SearchOneAsync(Statement condition, CancellationToken cancellationToken = default);

        Task<IList> SearchAsync(Statement condition, CancellationToken cancellationToken = default);

        Task<IList> SearchSectionAsync(Statement condition, SectionSet section, CancellationToken cancellationToken = default);

        Task<bool> ExistsKeyAsync(object[] keys, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(object o, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(Statement condition, CancellationToken cancellationToken = default);

        Task<int> CountAsync(Statement condition, CancellationToken cancellationToken = default);
    }
}