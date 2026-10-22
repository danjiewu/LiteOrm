using System;
using System.Linq;

namespace LiteOrm.Common
{
    /// <summary>
    /// <see cref="QuerySource{T1}"/> 的转换扩展。
    /// </summary>
    /// <remarks>
    /// <see cref="Queryable"/> 的算子（<c>Where</c> / <c>Select</c> / <c>OrderBy</c>）返回类型声明为
    /// <see cref="IQueryable{T}"/>，<c>OrderBy</c> / <c>ThenBy</c> 返回 <see cref="IOrderedQueryable{T}"/>，
    /// 静态类型上都看不到 <see cref="QuerySource{T1}"/> 的取数实例方法
    /// （<c>ToListAsync</c> / <c>CountAsync</c> / <c>ToAsyncEnumerable</c>）与 <see cref="QuerySource{T1}.GetQueryExpr"/>。
    /// <see cref="AsSource{T}(IQueryable{T})"/> 把静态类型收窄回 <see cref="QuerySource{T1}"/>，
    /// 运行时对象本来就是它，因此不重建、不复制表达式。
    /// </remarks>
    public static class QuerySourceExtensions
    {
        /// <summary>
        /// 把查询收窄为 <see cref="QuerySource{T}"/>，以便调用其取数实例方法与 <c>GetQueryExpr</c>。
        /// </summary>
        /// <typeparam name="T">元素类型。</typeparam>
        /// <param name="query">查询对象。</param>
        /// <returns>同一查询的 <see cref="QuerySource{T}"/> 视图。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="query"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">查询不是由 LiteOrm 查询源派生的。</exception>
        public static QuerySource<T> AsSource<T>(this IQueryable<T> query)
        {
            if (query is null) throw new ArgumentNullException(nameof(query));

            // 常见路径：Where / Select / OrderBy 的运行时对象仍是 QuerySource<T>，直接收窄
            if (query is QuerySource<T> source) return source;

            // 兜底：被包装过但 provider 仍是 LiteOrm 的，用原表达式重建。
            // 执行器可以为 null（Expr.From<T>() 的链），此时只用于拼装表达式，取数仍会抛 NoExecutorMessage
            if (query.Provider is QuerySourceProvider provider)
                return new QuerySource<T>(provider.Executor, query.Expression);

            throw new InvalidOperationException(
                "The query is not a LiteOrm QuerySource. " +
                "Build it from QuerySource<T> or ObjectViewDAO, or use ObjectViewDAO.SearchAs(IQueryable<T>).");
        }

        /// <summary>
        /// 把查询表达式转换为 <see cref="SqlSegment"/>，等价于 <see cref="QuerySource{T1}.GetQueryExpr"/>。
        /// </summary>
        /// <remarks>
        /// <see cref="Queryable"/> 算子之后静态类型退回 <see cref="IQueryable{T}"/> / <see cref="IOrderedQueryable{T}"/>，
        /// 看不到实例方法，因此这里补一个同名扩展，链尾可以直接调用。
        /// 不需要执行器，查询也可以没有绑定执行器。
        /// </remarks>
        /// <typeparam name="T">元素类型。</typeparam>
        /// <param name="query">查询对象，根必须是 <see cref="IQueryable{T}"/> 查询源。</param>
        /// <returns>解析后的查询段。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="query"/> 为 null。</exception>
        public static SqlSegment GetQueryExpr<T>(this IQueryable<T> query)
        {
            if (query is null) throw new ArgumentNullException(nameof(query));
            return QuerySourceExprConverter.ToExpr(query.Expression);
        }

        /// <summary>
        /// 把查询收窄为 <see cref="QuerySource{T}"/>，并在缺失执行器时补上指定的执行器。
        /// </summary>
        /// <typeparam name="T">元素类型。</typeparam>
        /// <param name="query">查询对象。</param>
        /// <param name="executor">查询执行器，通常来自 <c>ObjectViewDAO&lt;T&gt;</c> 或依赖注入容器。</param>
        /// <returns>绑定到 <paramref name="executor"/> 的查询源。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="query"/> 或 <paramref name="executor"/> 为 null。</exception>
        public static QuerySource<T> AsSource<T>(this IQueryable<T> query, IQuerySourceExecutor<T> executor)
        {
            if (query is null) throw new ArgumentNullException(nameof(query));
            if (executor is null) throw new ArgumentNullException(nameof(executor));

            // 已绑定执行器的查询源保持原样，避免覆盖调用方已经指定的执行器
            if (query is QuerySource<T> source
                && source.Provider is QuerySourceProvider { Executor: not null })
                return source;

            return new QuerySource<T>(executor, query.Expression);
        }
    }
}
