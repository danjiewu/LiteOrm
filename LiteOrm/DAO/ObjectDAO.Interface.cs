using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using LiteOrm.Common;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm
{
    public partial class ObjectDAO<T>
    {
        #region IObjectDAO Members

        bool IObjectDAO.Insert(object o)
        {
            return Insert((T)o);
        }
        void IObjectDAO.BatchInsert(IEnumerable values)
        {
            if (values is IEnumerable<T> typed)
                BatchInsert(typed);
            else
            {
                List<T> list = new List<T>();
                foreach (T entity in values)
                {
                    list.Add((T)entity);
                }
                BatchInsert(list);
            }
        }

        bool IObjectDAO.Update(object o)
        {
            return Update((T)o);
        }

        UpdateOrInsertResult IObjectDAO.UpdateOrInsert(object o)
        {
            return UpdateOrInsert((T)o);
        }

        void IObjectDAO.BatchUpdateOrInsert(IEnumerable values)
        {
            if (values is IEnumerable<T> typed)
                BatchUpdateOrInsert(typed);
            else
            {
                List<T> list = new List<T>();
                foreach (T entity in values)
                {
                    list.Add((T)entity);
                }
                BatchUpdateOrInsert(list);
            }
        }

        void IObjectDAO.BatchUpdate(IEnumerable values)
        {
            if (values is IEnumerable<T> typed)
                BatchUpdate(typed);
            else
            {
                List<T> list = new List<T>();
                foreach (T entity in values)
                {
                    list.Add((T)entity);
                }
                BatchUpdate(list);
            }
        }

        bool IObjectDAO.Delete(object o)
        {
            return Delete((T)o);
        }
        #endregion

        #region IObjectDAOAsync implementations

        async Task<bool> IObjectDAOAsync.InsertAsync(object o, CancellationToken cancellationToken)
        {
            return await InsertAsync((T)o, cancellationToken).ConfigureAwait(false);
        }

        async Task IObjectDAOAsync.BatchInsertAsync(IEnumerable values, CancellationToken cancellationToken)
        {
            if (values is IEnumerable<T> typed)
                await BatchInsertAsync(typed, cancellationToken).ConfigureAwait(false);
            else
            {
                List<T> list = new List<T>();
                foreach (T entity in values)
                {
                    list.Add((T)entity);
                }
                await BatchInsertAsync(list, cancellationToken).ConfigureAwait(false);
            }
        }

        async Task<bool> IObjectDAOAsync.UpdateAsync(object o, CancellationToken cancellationToken)
        {
            return await UpdateAsync((T)o, null, cancellationToken).ConfigureAwait(false);
        }

        async Task<UpdateOrInsertResult> IObjectDAOAsync.UpdateOrInsertAsync(object o, CancellationToken cancellationToken)
        {
            return await UpdateOrInsertAsync((T)o, cancellationToken).ConfigureAwait(false);
        }

        async Task IObjectDAOAsync.BatchUpdateAsync(IEnumerable values, CancellationToken cancellationToken)
        {
            if (values is IEnumerable<T> typed)
                await BatchUpdateAsync(typed, cancellationToken).ConfigureAwait(false);
            else
            {
                List<T> list = new List<T>();
                foreach (T entity in values)
                {
                    list.Add((T)entity);
                }
                await BatchUpdateAsync(list, cancellationToken).ConfigureAwait(false);
            }
        }

        async Task IObjectDAOAsync.BatchUpdateOrInsertAsync(IEnumerable values, CancellationToken cancellationToken)
        {
            if (values is IEnumerable<T> typed)
                await BatchUpdateOrInsertAsync(typed, cancellationToken).ConfigureAwait(false);
            else
            {
                List<T> list = new List<T>();
                foreach (T entity in values)
                {
                    list.Add((T)entity);
                }
                await BatchUpdateOrInsertAsync(list, cancellationToken).ConfigureAwait(false);
            }
        }

        async Task<bool> IObjectDAOAsync.DeleteAsync(object o, CancellationToken cancellationToken)
        {
            return await DeleteAsync((T)o, cancellationToken).ConfigureAwait(false);
        }

        async Task<bool> IObjectDAOAsync.DeleteByKeysAsync(object[] keys, CancellationToken cancellationToken)
        {
            return await DeleteByKeysAsync(keys, cancellationToken).ConfigureAwait(false);
        }

        async Task<int> IObjectDAOAsync.DeleteAsync(Expr expr, CancellationToken cancellationToken)
        {
            return await DeleteAsync(expr, cancellationToken).ConfigureAwait(false);
        }

        #endregion
    }
}
