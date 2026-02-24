using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Common
{
    /// <summary>
    /// 异步版：实体视图查询操作的泛型接口
    /// </summary>
    /// <typeparam name="T">实体类类型</typeparam>
    public interface IObjectViewDAOAsync<T> : IObjectViewDAOAsync
    {
        /// <summary>
        /// 异步根据主键获取对象
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回找到的对象，如果未找到则返回null</returns>
        new Task<T> GetObjectAsync(object[] keys, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件查询单个对象
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回找到的对象，如果未找到则返回null</returns>
        new Task<T> SearchOneAsync(Expr expr, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步遍历符合条件的对象并对每个对象执行指定操作
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="func">要对每个对象执行的操作</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务</returns>
        Task ForEachAsync(Expr expr, Func<T, Task> func, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件查询对象列表
        /// </summary>
        /// <param name="expr">查询条件，如果为null则查询所有对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回对象列表</returns>
        new Task<List<T>> SearchAsync(Expr expr = null, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 异步版：实体视图查询操作的非泛型接口
    /// </summary>
    [AutoRegister(false)]
    public interface IObjectViewDAOAsync
    {
        /// <summary>
        /// 异步根据主键获取对象
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回找到的对象，如果未找到则返回null</returns>
        Task<object> GetObjectAsync(object[] keys, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件查询单个对象
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回找到的对象，如果未找到则返回null</returns>
        Task<object> SearchOneAsync(Expr expr, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据条件查询对象列表
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回对象列表</returns>
        Task<IList> SearchAsync(Expr expr, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步检查指定主键的对象是否存在
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回对象是否存在</returns>
        Task<bool> ExistsKeyAsync(object[] keys, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步检查指定对象是否存在（根据主键判断）
        /// </summary>
        /// <param name="o">要检查的对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回对象是否存在</returns>
        Task<bool> ExistsAsync(object o, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步检查符合条件的对象是否存在
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回符合条件的对象是否存在</returns>
        Task<bool> ExistsAsync(Expr expr, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步统计符合条件的对象数量
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，返回对象数量</returns>
        Task<int> CountAsync(Expr expr, CancellationToken cancellationToken = default);
    }
}
