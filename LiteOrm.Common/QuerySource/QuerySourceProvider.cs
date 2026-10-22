using System;
using System.Linq;
using System.Linq.Expressions;

namespace LiteOrm.Common
{
    /// <summary>
    /// <see cref="QuerySource{T1}"/> 的 <see cref="IQueryProvider"/> 实现。
    /// 独立成非泛型类型，避免为每个元素类型生成一个 provider 类型，AOT 下实例化面更小。
    /// </summary>
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2091", Justification = "TResult is the element type of the query the caller already constructed; entity metadata is preserved through [Table] entities and the source generator.")]
#endif
    internal sealed class QuerySourceProvider : IQueryProvider
    {
        private readonly IQuerySourceExecutor? _executor;

        /// <summary>
        /// 关联的查询执行器，未设置时为 null。
        /// </summary>
        internal IQuerySourceExecutor? Executor => _executor;

        /// <summary>
        /// 初始化 <see cref="QuerySourceProvider"/> 的新实例。
        /// </summary>
        /// <param name="executor">查询执行器，可为 null（此时仅用于解析表达式）。</param>
        public QuerySourceProvider(IQuerySourceExecutor? executor)
        {
            _executor = executor;
        }

        /// <inheritdoc/>
        public IQueryable CreateQuery(Expression expression)
            => throw new NotSupportedException("Non-generic IQueryProvider.CreateQuery is not supported. Use the generic Queryable operators.");

        /// <inheritdoc/>
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            => new QuerySource<TElement>(_executor, expression);

        /// <inheritdoc/>
        public object? Execute(Expression expression)
            => throw new NotSupportedException("Non-generic IQueryProvider.Execute is not supported. Use the generic terminals.");

        /// <inheritdoc/>
        public TResult Execute<TResult>(Expression expression)
        {
            IQuerySourceExecutor executor = _executor
                ?? throw new InvalidOperationException(QuerySource<object>.NoExecutorMessage);

            // 终结符（First / FirstOrDefault / Single ...）不改变 SQL 形态，先剥离
            Expression body = QuerySourceExprConverter.UnwrapTerminal(expression, out bool single);
            SqlSegment plan = QuerySourceExprConverter.ToExpr(body);

            return single
                ? executor.FirstOrDefault<TResult>(plan)
                : executor.AsEnumerable<TResult>(plan).FirstOrDefault()!;
        }
    }
}