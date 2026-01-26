using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Collections;
using LiteOrm.Common;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm
{
    public partial class ObjectViewDAO<T> where T : new()
    {
        #region IObjectViewDAO Members

        /// <summary>
        /// 根据主键获取对象（接口实现）
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <returns>对象，若存在则返回null</returns>
        object IObjectViewDAO.GetObject(params object[] keys)
        {
            return GetObject(keys);
        }

        /// <summary>
        /// 获取单个符合条件的对象（接口实现）
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <returns>第一个符合条件的对象，若不存在则返回null</returns>
        object IObjectViewDAO.SearchOne(Expr expr)
        {
            return SearchOne(expr);
        }

        /// <summary>
        /// 根据条件查询，多个条件以逻辑与连接（接口实现）
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <returns>符合条件的对象列表</returns>
        IList IObjectViewDAO.Search(Expr expr)
        {
            return Search(expr);
        }

        /// <summary>
        /// 根据条件查询，多个条件以逻辑与连接（接口实现）
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <param name="orderBy">排列顺序，若为null则表示不指定顺序</param>
        /// <returns>符合条件的对象列表</returns>
        IList IObjectViewDAO.Search(Expr expr, params Sorting[] orderBy)
        {
            return Search(expr, orderBy);
        }

        /// <summary>
        /// 分页查询（接口实现）
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="section">分页设定</param>
        /// <returns>分页查询结果</returns>
        IList IObjectViewDAO.SearchSection(Expr expr, PageSection section)
        {
            return SearchSection(expr, section);
        }

        #endregion

        #region IObjectViewDAOAsync implementations

        /// <summary>
        /// 根据主键异步获取对象（接口实现）
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含对象，若不存在则返回null</returns>
        async Task<object> IObjectViewDAOAsync.GetObjectAsync(object[] keys, CancellationToken cancellationToken)
        {
            return await GetObjectAsync(keys, cancellationToken);
        }

        /// <summary>
        /// 异步获取单个符合条件的对象（接口实现）
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含第一个符合条件的对象，若不存在则返回null</returns>
        async Task<object> IObjectViewDAOAsync.SearchOneAsync(Expr expr, CancellationToken cancellationToken)
        {
            return await SearchOneAsync(expr, cancellationToken);
        }

        /// <summary>
        /// 异步根据条件查询，多个条件以逻辑与连接（接口实现）
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含符合条件的对象列表</returns>
        async Task<IList> IObjectViewDAOAsync.SearchAsync(Expr expr, CancellationToken cancellationToken)
        {
            return await SearchAsync(expr, cancellationToken);
        }

        /// <summary>
        /// 异步根据条件查询，多个条件以逻辑与连接（接口实现）
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <param name="orderBy">排列顺序，若为null则表示不指定顺序</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含符合条件的对象列表</returns>
        async Task<IList> IObjectViewDAOAsync.SearchAsync(Expr expr, Sorting[] orderBy, CancellationToken cancellationToken)
        {
            return await SearchAsync(expr, orderBy, cancellationToken);
        }

        /// <summary>
        /// 异步分页查询（接口实现）
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="section">分页设定</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含分页查询结果</returns>
        async Task<IList> IObjectViewDAOAsync.SearchSectionAsync(Expr expr, PageSection section, CancellationToken cancellationToken)
        {
            return await SearchSectionAsync(expr, section, cancellationToken);
        }

        #endregion
    }
}
