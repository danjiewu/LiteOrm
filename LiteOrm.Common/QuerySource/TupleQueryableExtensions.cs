using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;

namespace LiteOrm.Common
{
    /// <summary>
    /// 为元组查询源提供多参数谓词扩展。
    /// </summary>
    /// <remarks>
    /// 连接查询的元素类型是 <c>(T1, T2, ...)</c>，直接写 <c>Where</c> 时只能使用
    /// <c>x =&gt; x.Item1...</c> / <c>x =&gt; x.Item2...</c>。本扩展允许写成
    /// <c>Where((t1, t2, ...) =&gt; ...)</c>，内部会重写为单参数元组 Lambda，
    /// 因此现有的元组元素解析逻辑无需改动。
    /// </remarks>
    public static class TupleQueryableExtensions
    {
        /// <summary>
        /// 使用两个独立参数筛选元组查询源。
        /// </summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="predicate">接收两个元素的谓词。</param>
        /// <returns>筛选后的查询。</returns>
        /// <example>
        /// <code>
        /// var query = Expr.From&lt;Order&gt;()
        ///     .JoinRelated&lt;Customer&gt;()
        ///     .Where((o, c) =&gt; o.Amount &gt; 10 &amp;&amp; c.Name == "VIP");
        /// </code>
        /// </example>
        public static IQueryable<(T1, T2)> Where<T1, T2>(
            this IQueryable<(T1, T2)> source,
            Expression<Func<T1, T2, bool>> predicate)
            => WhereCore(source, predicate);

        /// <summary>
        /// 使用三个独立参数筛选元组查询源。
        /// </summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="predicate">接收三个元素的谓词。</param>
        /// <returns>筛选后的查询。</returns>
        public static IQueryable<(T1, T2, T3)> Where<T1, T2, T3>(
            this IQueryable<(T1, T2, T3)> source,
            Expression<Func<T1, T2, T3, bool>> predicate)
            => WhereCore(source, predicate);

        /// <summary>
        /// 使用四个独立参数筛选元组查询源。
        /// </summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="predicate">接收四个元素的谓词。</param>
        /// <returns>筛选后的查询。</returns>
        public static IQueryable<(T1, T2, T3, T4)> Where<T1, T2, T3, T4>(
            this IQueryable<(T1, T2, T3, T4)> source,
            Expression<Func<T1, T2, T3, T4, bool>> predicate)
            => WhereCore(source, predicate);

        /// <summary>
        /// 使用五个独立参数筛选元组查询源。
        /// </summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="T5">元组第五个元素的类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="predicate">接收五个元素的谓词。</param>
        /// <returns>筛选后的查询。</returns>
        public static IQueryable<(T1, T2, T3, T4, T5)> Where<T1, T2, T3, T4, T5>(
            this IQueryable<(T1, T2, T3, T4, T5)> source,
            Expression<Func<T1, T2, T3, T4, T5, bool>> predicate)
            => WhereCore(source, predicate);

        /// <summary>
        /// 使用六个独立参数筛选元组查询源。
        /// </summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="T5">元组第五个元素的类型。</typeparam>
        /// <typeparam name="T6">元组第六个元素的类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="predicate">接收六个元素的谓词。</param>
        /// <returns>筛选后的查询。</returns>
        public static IQueryable<(T1, T2, T3, T4, T5, T6)> Where<T1, T2, T3, T4, T5, T6>(
            this IQueryable<(T1, T2, T3, T4, T5, T6)> source,
            Expression<Func<T1, T2, T3, T4, T5, T6, bool>> predicate)
            => WhereCore(source, predicate);

        /// <summary>
        /// 使用七个独立参数筛选元组查询源。
        /// </summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="T5">元组第五个元素的类型。</typeparam>
        /// <typeparam name="T6">元组第六个元素的类型。</typeparam>
        /// <typeparam name="T7">元组第七个元素的类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="predicate">接收七个元素的谓词。</param>
        /// <returns>筛选后的查询。</returns>
        public static IQueryable<(T1, T2, T3, T4, T5, T6, T7)> Where<T1, T2, T3, T4, T5, T6, T7>(
            this IQueryable<(T1, T2, T3, T4, T5, T6, T7)> source,
            Expression<Func<T1, T2, T3, T4, T5, T6, T7, bool>> predicate)
            => WhereCore(source, predicate);

        /// <summary>
        /// 把多参数谓词重写为标准单参数元组谓词，再交给 <see cref="Queryable.Where{TSource}(IQueryable{TSource}, Expression{Func{TSource, bool}})"/>。
        /// </summary>
        private static IQueryable<TTuple> WhereCore<TTuple>(
            IQueryable<TTuple> source,
            LambdaExpression predicate)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (predicate is null) throw new ArgumentNullException(nameof(predicate));

            return Queryable.Where(source, RewriteTuplePredicate<TTuple>(predicate));
        }

        /// <summary>
        /// 把谓词的多个形参依次替换为元组的 <c>Item1</c> ~ <c>Item7</c> 字段访问。
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2026",
            Justification = "ValueTuple<T1..T7>.Item1..Item7 are fixed public fields on a closed generic type used by the query; they are not trimmed.")]
        private static Expression<Func<TTuple, bool>> RewriteTuplePredicate<TTuple>(LambdaExpression predicate)
        {
            ParameterExpression tupleParameter = Expression.Parameter(typeof(TTuple), "tuple");
            var replacements = new Dictionary<ParameterExpression, Expression>(predicate.Parameters.Count);
            for (int i = 0; i < predicate.Parameters.Count; i++)
            {
                replacements[predicate.Parameters[i]] = Expression.Field(tupleParameter, "Item" + (i + 1));
            }

            Expression body = new ParameterReplacer(replacements).Visit(predicate.Body)!;
            return Expression.Lambda<Func<TTuple, bool>>(body, tupleParameter);
        }

        /// <summary>
        /// 把谓词中的参数替换为元组字段访问。
        /// </summary>
        private sealed class ParameterReplacer : ExpressionVisitor
        {
            private readonly Dictionary<ParameterExpression, Expression> _replacements;

            public ParameterReplacer(Dictionary<ParameterExpression, Expression> replacements)
                => _replacements = replacements;

            /// <inheritdoc/>
            protected override Expression VisitParameter(ParameterExpression node)
                => _replacements.TryGetValue(node, out Expression? replacement)
                    ? replacement
                    : base.VisitParameter(node);
        }
    }
}
