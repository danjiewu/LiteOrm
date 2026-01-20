using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Linq.Expressions;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Common
{
    /// <summary>
    /// 实体类的增删改等基本操作的异步泛型接口
    /// </summary>
    /// <typeparam name="T">实体类类型</typeparam>
    public interface IObjectDAOAsync<T>
    {
        /// <summary>
        /// 异步添加对象
        /// </summary>
        /// <param name="o">待添加的对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回是否成功添加</returns>
        Task<bool> InsertAsync(T o, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 异步批量添加对象
        /// </summary>
        /// <param name="values">待添加的对象集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务</returns>
        Task BatchInsertAsync(IEnumerable<T> values, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 异步更新对象
        /// </summary>
        /// <param name="o">待更新的对象</param>
        /// <param name="timestamp">时间戳</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回是否成功更新</returns>
        Task<bool> UpdateAsync(T o, object timestamp = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 异步更新或添加对象，若存在则更新，若不存在则添加
        /// </summary>
        /// <param name="o">待更新或添加的对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回更新或添加的结果</returns>
        Task<UpdateOrInsertResult> UpdateOrInsertAsync(T o, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 异步删除对象
        /// </summary>
        /// <param name="o">待删除的对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回是否成功删除</returns>
        Task<bool> DeleteAsync(T o, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 异步根据条件删除对象
        /// </summary>
        /// <param name="expr">条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回删除对象数量</returns>
        Task<int> DeleteAsync(Expr expr, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 异步根据主键删除对象
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回是否成功删除</returns>
        Task<bool> DeleteByKeysAsync(object[] keys, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 实体类的增删改等基本操作的异步非泛型接口
    /// </summary>
    [AutoRegister(false)]
    public interface IObjectDAOAsync
    {
        /// <summary>
        /// 异步添加对象
        /// </summary>
        /// <param name="o">待添加的对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回是否成功添加</returns>
        Task<bool> InsertAsync(object o, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 异步批量添加对象
        /// </summary>
        /// <param name="values">待添加的对象集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务</returns>
        Task BatchInsertAsync(IEnumerable values, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 异步更新对象
        /// </summary>
        /// <param name="o">待更新的对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回是否成功更新</returns>
        Task<bool> UpdateAsync(object o, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 异步更新或添加对象，若存在则更新，若不存在则添加
        /// </summary>
        /// <param name="o">待更新或添加的对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回更新或添加的结果</returns>
        Task<UpdateOrInsertResult> UpdateOrInsertAsync(object o, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 异步根据条件更新数据
        /// </summary>
        /// <param name="values">需要更新的属性及数值，key为属性名，value为数值</param>
        /// <param name="expr">更新的条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回更新的记录数</returns>
        Task<int> UpdateAllValuesAsync(IEnumerable<KeyValuePair<string, object>> values, Expr expr, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 异步删除对象
        /// </summary>
        /// <param name="o">待删除的对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回是否成功删除</returns>
        Task<bool> DeleteAsync(object o, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 异步根据主键删除对象
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回是否成功删除</returns>
        Task<bool> DeleteByKeysAsync(object[] keys, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件删除对象
        /// </summary>
        /// <param name="expr">条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回删除对象数量</returns>
        Task<int> DeleteAsync(Expr expr, CancellationToken cancellationToken = default);
    }
}
