using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Common
{
    /// <summary>
    /// 查询源执行器。由 ORM 层实现（<c>ObjectViewDAO&lt;T&gt;</c>），
    /// 使 <see cref="QuerySource{T1}"/> 在持有执行器时可以直接取数。
    /// </summary>
    /// <remarks>
    /// 方法均为泛型：<see cref="QuerySource{T1}"/> 经过 Select 投影后元素类型会变成 TResult，
    /// 但仍需由同一个执行器执行，因此执行器本身不做泛型。
    /// 异步接口使用 <see cref="Task"/> 与 <see cref="IAsyncEnumerable{T}"/>，netstandard2.0 下由
    /// Microsoft.Bcl.AsyncInterfaces 提供。
    /// </remarks>
    public interface IQuerySourceExecutor
    {
        /// <summary>
        /// 流式执行查询，按行返回结果。
        /// </summary>
        /// <typeparam name="TResult">结果元素类型。</typeparam>
        /// <param name="query">要执行的查询表达式。</param>
        /// <returns>惰性读取的结果序列。</returns>
        IEnumerable<TResult> AsEnumerable<[DynamicallyAccessedMembers(Constants.RegistedMemberTypes)] TResult>(Expr query);

        /// <summary>
        /// 异步流式执行查询，按行返回结果。
        /// </summary>
        /// <typeparam name="TResult">结果元素类型。</typeparam>
        /// <param name="query">要执行的查询表达式。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步流式读取的结果序列。</returns>
        IAsyncEnumerable<TResult> AsAsyncEnumerable<[DynamicallyAccessedMembers(Constants.RegistedMemberTypes)] TResult>(Expr query, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步执行查询并一次性返回结果列表。
        /// </summary>
        /// <typeparam name="TResult">结果元素类型。</typeparam>
        /// <param name="query">要执行的查询表达式。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>结果列表。</returns>
        Task<List<TResult>> ToListAsync<[DynamicallyAccessedMembers(Constants.RegistedMemberTypes)] TResult>(Expr query, CancellationToken cancellationToken = default);

        /// <summary>
        /// 同步取第一行，无数据时返回默认值。
        /// </summary>
        /// <typeparam name="TResult">结果元素类型。</typeparam>
        /// <param name="query">要执行的查询表达式。</param>
        /// <returns>第一行结果，无数据时为默认值。</returns>
        TResult FirstOrDefault<[DynamicallyAccessedMembers(Constants.RegistedMemberTypes)] TResult>(Expr query);

        /// <summary>
        /// 异步取第一行，无数据时返回默认值。
        /// </summary>
        /// <typeparam name="TResult">结果元素类型。</typeparam>
        /// <param name="query">要执行的查询表达式。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>第一行结果，无数据时为默认值。</returns>
        Task<TResult> FirstOrDefaultAsync<[DynamicallyAccessedMembers(Constants.RegistedMemberTypes)] TResult>(Expr query, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 查询源执行器的泛型版本，把结果元素类型固定在类型参数上。
    /// 由 ORM 层实现（<c>ObjectViewDAO&lt;T&gt;</c>），<see cref="QuerySource{T1}"/> 通过它直接取数。
    /// </summary>
    /// <typeparam name="T">结果元素类型。</typeparam>
    public interface IQuerySourceExecutor<T> : IQuerySourceExecutor
    {
        /// <summary>
        /// 流式执行查询，按行返回 <typeparamref name="T"/>。
        /// </summary>
        /// <param name="query">要执行的查询表达式。</param>
        /// <returns>惰性读取的结果序列。</returns>
        IEnumerable<T> AsEnumerable(Expr query);

        /// <summary>
        /// 异步流式执行查询，按行返回 <typeparamref name="T"/>。
        /// </summary>
        /// <param name="query">要执行的查询表达式。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步流式读取的结果序列。</returns>
        IAsyncEnumerable<T> AsAsyncEnumerable(Expr query, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步执行查询并一次性返回结果列表。
        /// </summary>
        /// <param name="query">要执行的查询表达式。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>结果列表。</returns>
        Task<List<T>> ToListAsync(Expr query, CancellationToken cancellationToken = default);

        /// <summary>
        /// 同步取第一行，无数据时返回默认值。
        /// </summary>
        /// <param name="query">要执行的查询表达式。</param>
        /// <returns>第一行结果，无数据时为默认值。</returns>
        T FirstOrDefault(Expr query);

        /// <summary>
        /// 异步取第一行，无数据时返回默认值。
        /// </summary>
        /// <param name="query">要执行的查询表达式。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>第一行结果，无数据时为默认值。</returns>
        Task<T> FirstOrDefaultAsync(Expr query, CancellationToken cancellationToken = default);
    }
}