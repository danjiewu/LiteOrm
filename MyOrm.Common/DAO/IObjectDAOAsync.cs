using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Linq.Expressions;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace MyOrm.Common
{
    public interface IObjectDAOAsync<T>
    {
        Task<bool> InsertAsync(T o, CancellationToken cancellationToken = default);
        Task BatchInsertAsync(IEnumerable<T> values, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(T o, object timestamp = null, CancellationToken cancellationToken = default);
        Task<UpdateOrInsertResult> UpdateOrInsertAsync(T o, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(T o, CancellationToken cancellationToken = default);
        Task<int> DeleteAsync(Statement condition, CancellationToken cancellationToken = default);
        Task<bool> DeleteByKeysAsync(object[] keys, CancellationToken cancellationToken = default);
    }

    [AutoRegister(false)]
    public interface IObjectDAOAsync
    {
        Task<bool> InsertAsync(object o, CancellationToken cancellationToken = default);
        Task BatchInsertAsync(IEnumerable values, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(object o, CancellationToken cancellationToken = default);
        Task<UpdateOrInsertResult> UpdateOrInsertAsync(object o, CancellationToken cancellationToken = default);
        Task<int> UpdateAllValuesAsync(IEnumerable<KeyValuePair<string, object>> values, Statement condition, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(object o, CancellationToken cancellationToken = default);
        Task<bool> DeleteByKeysAsync(object[] keys, CancellationToken cancellationToken = default);
        Task<int> DeleteAsync(Statement condition, CancellationToken cancellationToken = default);
    }
}
