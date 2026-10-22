using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;

namespace LiteOrm.Common
{
    /// <summary>
    /// 把基于 <see cref="IQueryable{T}"/> 的查询转换为 <see cref="Expr"/>。
    /// 根可以是任意 <see cref="IQueryable{T}"/>（不要求是 <see cref="QuerySource{T1}"/>），
    /// 表类型取自查询源的元素类型。
    /// </summary>
    /// <remarks>
    /// 转换方式是把表达式树里的根 <see cref="ConstantExpression"/> 改写成
    /// <see cref="ParameterExpression"/>，再合成 Lambda 交给 <see cref="LambdaExprConverter"/>，
    /// 从而完整复用已有的 Where / Select / OrderBy / GroupBy / 聚合 / Join 解析逻辑。
    /// </remarks>
    public static class QuerySourceExprConverter
    {
        /// <summary>
        /// 把查询转换为 <see cref="SqlSegment"/>。
        /// </summary>
        /// <param name="query">查询对象。</param>
        /// <returns>转换后的 SQL 片段。</returns>
        public static SqlSegment ToExpr(IQueryable query)
        {
            if (query is null) throw new ArgumentNullException(nameof(query));
            return ToExpr(query.Expression);
        }

        /// <summary>
        /// 把查询表达式转换为 <see cref="SqlSegment"/>。
        /// </summary>
        /// <param name="expression">查询表达式。</param>
        /// <returns>转换后的 SQL 片段。</returns>
        public static SqlSegment ToExpr(Expression expression)
        {
            if (expression is null) throw new ArgumentNullException(nameof(expression));

            // 先剥离不影响 SQL 形态的终结符（ToList / First / Single ...），
            // 否则链尾的 Enumerable.ToList 会落到 DefaultFunctionHandler 生成伪函数
            expression = UnwrapTerminal(expression, out _);

            var rewriter = new RootRewriter();
            Expression body = rewriter.Visit(expression)!;
            if (rewriter.Parameter is null)
                throw new ArgumentException(
                    "The expression is not rooted at an IQueryable query source and cannot be converted to Expr.",
                    nameof(expression));

            // 直接用 body + 参数构造转换器，不合成 LambdaExpression：
            // AOT 下 Expression.Lambda 需要动态生成委托类型（IL3050）
            SqlSegment segment = (SqlSegment)new LambdaExprConverter(body, rewriter.Parameter).ToSqlSegment();
            ApplyTableArgs(segment, rewriter.TableArgs);
            return segment;
        }

        /// <summary>
        /// 把根 <see cref="ConstantExpression"/> 替换成 <see cref="IQueryable{T}"/> 参数，
        /// 同时记录 <see cref="QuerySource{T1}"/> 携带的分表参数。
        /// </summary>
        private sealed class RootRewriter : ExpressionVisitor
        {
            /// <summary>改写后的查询根参数。</summary>
            public ParameterExpression? Parameter { get; private set; }

            /// <summary>查询根携带的分表参数。</summary>
            public string[]? TableArgs { get; private set; }

            /// <inheritdoc/>
#if NET8_0_OR_GREATER
            [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "ConstantExpression.Type is a runtime value; the query root types come from user query sources whose entity metadata is preserved via [Table] entities and the source generator.")]
#endif
            protected override Expression VisitConstant(ConstantExpression node)
            {
                // 任意 IQueryable 都能作为查询根，表类型取 IQueryable<T> 的元素类型
                if (node.Value is IQueryable)
                {
                    if (Parameter is not null)
                        throw new NotSupportedException("Only one query root is supported in a query expression.");

                    Parameter = Expression.Parameter(QueryableInterface(node.Type), "__source");
                    // 只有 QuerySource<T> 才带分表参数，其他 IQueryable 源没有
                    TableArgs = (node.Value as IQuerySource)?.TableArgs;
                    return Parameter;
                }
                return base.VisitConstant(node);
            }
        }

        /// <summary>
        /// 从类型上找出 <see cref="IQueryable{T}"/> 接口。
        /// </summary>
        private static Type QueryableInterface([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IQueryable<>)) return type;
            foreach (Type candidate in type.GetInterfaces())
            {
                if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IQueryable<>))
                    return candidate;
            }
            throw new NotSupportedException($"Type '{type}' does not implement IQueryable<T>.");
        }

        /// <summary>
        /// 把 <see cref="Expr.From{T}(string[])"/> 的分表参数回填到根表。
        /// </summary>
        private static void ApplyTableArgs(SqlSegment segment, string[]? tableArgs)
        {
            if (tableArgs is null || tableArgs.Length == 0) return;

            SqlSegment current = segment;
            while (current.Source is SqlSegment next) current = next;
            if (current is TableExpr table && (table.TableArgs is null || table.TableArgs.Length == 0))
                table.TableArgs = tableArgs;
        }

        /// <summary>
        /// 剥离不改变 SQL 形态的终结符。返回剥离后的表达式，并通过 <paramref name="single"/>
        /// 说明是否只取一行。
        /// </summary>
        /// <param name="expression">查询表达式。</param>
        /// <param name="single">是否只需返回一行。</param>
        /// <returns>剥离终结符后的表达式。</returns>
        internal static Expression UnwrapTerminal(Expression expression, out bool single)
        {
            single = false;

            while (expression is MethodCallExpression call
                   && (call.Method.DeclaringType == typeof(Queryable) || call.Method.DeclaringType == typeof(Enumerable))
                   && IsPassThroughTerminal(call.Method.Name))
            {
                if (IsSingleRowTerminal(call.Method.Name)) single = true;
                expression = call.Arguments[0];
            }

            if (expression is MethodCallExpression unsupported
                && unsupported.Method.DeclaringType == typeof(Queryable)
                && !IsSupportedOperator(unsupported.Method.Name))
                throw new NotSupportedException(
                    $"LINQ operator '{unsupported.Method.Name}' is not supported by LiteOrm QuerySource. " +
                    "Supported: Where/Select/OrderBy/ThenBy/Skip/Take/GroupBy and Count/Sum/Average/Max/Min.");

            return expression;
        }

        /// <summary>判断是否为转换器已实现的 Queryable 算子。</summary>
        private static bool IsSupportedOperator(string methodName)
            => methodName is "Where" or "Select" or "OrderBy" or "OrderByDescending"
                or "ThenBy" or "ThenByDescending" or "Skip" or "Take" or "GroupBy"
                or "Count" or "LongCount" or "Sum" or "Average" or "Min" or "Max";

        /// <summary>判断是否为可剥离的终结符。</summary>
        private static bool IsPassThroughTerminal(string methodName)
            => methodName == nameof(Queryable.AsQueryable)
               || methodName == nameof(Enumerable.AsEnumerable)
               || methodName == nameof(Enumerable.ToList)
               || methodName == nameof(Enumerable.ToArray)
               || IsSingleRowTerminal(methodName);

        /// <summary>判断是否为只取一行的终结符。</summary>
        private static bool IsSingleRowTerminal(string methodName)
            => methodName == nameof(Queryable.First)
               || methodName == nameof(Queryable.FirstOrDefault)
               || methodName == nameof(Queryable.Single)
               || methodName == nameof(Queryable.SingleOrDefault);
    }
}