using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#if NET8_0_OR_GREATER
using LiteOrm.Common;
#endif

namespace LiteOrm.Common
{
    /// <summary>
    /// 异步版：实体视图查询操作的泛型接口
    /// </summary>
    /// <typeparam name="T">实体类类型</typeparam>
    public interface IObjectViewDAOAsync<T> : IObjectViewDAOAsync
    {
        /// <summary>
        /// 根据主键获取对象
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>可枚举结果对象，可通过FirstOrDefault()和FirstOrDefaultAsync()获取结果</returns>
        new EnumerableResult<T> GetObjectAsync(object[] keys, CancellationToken cancellationToken = default);

        /// <summary>
        /// 根据条件查询单个对象
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>可枚举结果对象，可通过FirstOrDefault()和FirstOrDefaultAsync()获取结果</returns>
        new EnumerableResult<T> SearchOneAsync(Expr expr, CancellationToken cancellationToken = default);

#if NET8_0_OR_GREATER

#endif
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



#if NET8_0_OR_GREATER

#endif

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




    }
}
