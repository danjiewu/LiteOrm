using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;

namespace LiteOrm.Common
{
    /// <summary>
    /// 为元组查询源提供多参数查询算子扩展。
    /// </summary>
    /// <remarks>
    /// 连接查询的元素类型是 <c>(T1, T2, ...)</c>，直接写 <c>Where</c> 时只能使用
    /// <c>x =&gt; x.Item1...</c> / <c>x =&gt; x.Item2...</c>。本扩展允许写成
    /// <c>Where((t1, t2, ...) =&gt; ...)</c>、<c>Select((t1, t2) =&gt; ...)</c>、
    /// <c>OrderBy((t1, t2) =&gt; ...)</c>、<c>GroupBy((t1, t2) =&gt; ...)</c> 等多参数形式，
    /// 内部会重写为单参数元组 Lambda，因此现有的元组元素解析逻辑无需改动。
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

            return Queryable.Where(source, RewriteTupleLambda<TTuple, bool>(predicate));
        }

        /// <summary>
        /// 把 Lambda 的多个形参依次替换为元组的 <c>Item1</c> ~ <c>Item7</c> 字段访问。
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2026",
            Justification = "ValueTuple<T1..T7>.Item1..Item7 are fixed public fields on a closed generic type used by the query; they are not trimmed.")]
        private static Expression<Func<TTuple, TResult>> RewriteTupleLambda<TTuple, TResult>(LambdaExpression lambda)
        {
            ParameterExpression tupleParameter = Expression.Parameter(typeof(TTuple), "tuple");
            var replacements = new Dictionary<ParameterExpression, Expression>(lambda.Parameters.Count);
            for (int i = 0; i < lambda.Parameters.Count; i++)
            {
                replacements[lambda.Parameters[i]] = Expression.Field(tupleParameter, "Item" + (i + 1));
            }

            Expression body = new ParameterReplacer(replacements).Visit(lambda.Body)!;
            return Expression.Lambda<Func<TTuple, TResult>>(body, tupleParameter);
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

        #region Select

        /// <summary>使用两个独立参数投影元组查询源。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="TResult">投影结果类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="selector">接收两个元素的投影表达式。</param>
        /// <returns>投影后的查询。</returns>
        public static IQueryable<TResult> Select<T1, T2, TResult>(
            this IQueryable<(T1, T2)> source,
            Expression<Func<T1, T2, TResult>> selector)
            => SelectCore<(T1, T2), TResult>(source, selector);

        /// <summary>使用三个独立参数投影元组查询源。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="TResult">投影结果类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="selector">接收三个元素的投影表达式。</param>
        /// <returns>投影后的查询。</returns>
        public static IQueryable<TResult> Select<T1, T2, T3, TResult>(
            this IQueryable<(T1, T2, T3)> source,
            Expression<Func<T1, T2, T3, TResult>> selector)
            => SelectCore<(T1, T2, T3), TResult>(source, selector);

        /// <summary>使用四个独立参数投影元组查询源。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="TResult">投影结果类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="selector">接收四个元素的投影表达式。</param>
        /// <returns>投影后的查询。</returns>
        public static IQueryable<TResult> Select<T1, T2, T3, T4, TResult>(
            this IQueryable<(T1, T2, T3, T4)> source,
            Expression<Func<T1, T2, T3, T4, TResult>> selector)
            => SelectCore<(T1, T2, T3, T4), TResult>(source, selector);

        /// <summary>使用五个独立参数投影元组查询源。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="T5">元组第五个元素的类型。</typeparam>
        /// <typeparam name="TResult">投影结果类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="selector">接收五个元素的投影表达式。</param>
        /// <returns>投影后的查询。</returns>
        public static IQueryable<TResult> Select<T1, T2, T3, T4, T5, TResult>(
            this IQueryable<(T1, T2, T3, T4, T5)> source,
            Expression<Func<T1, T2, T3, T4, T5, TResult>> selector)
            => SelectCore<(T1, T2, T3, T4, T5), TResult>(source, selector);

        /// <summary>使用六个独立参数投影元组查询源。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="T5">元组第五个元素的类型。</typeparam>
        /// <typeparam name="T6">元组第六个元素的类型。</typeparam>
        /// <typeparam name="TResult">投影结果类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="selector">接收六个元素的投影表达式。</param>
        /// <returns>投影后的查询。</returns>
        public static IQueryable<TResult> Select<T1, T2, T3, T4, T5, T6, TResult>(
            this IQueryable<(T1, T2, T3, T4, T5, T6)> source,
            Expression<Func<T1, T2, T3, T4, T5, T6, TResult>> selector)
            => SelectCore<(T1, T2, T3, T4, T5, T6), TResult>(source, selector);

        /// <summary>使用七个独立参数投影元组查询源。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="T5">元组第五个元素的类型。</typeparam>
        /// <typeparam name="T6">元组第六个元素的类型。</typeparam>
        /// <typeparam name="T7">元组第七个元素的类型。</typeparam>
        /// <typeparam name="TResult">投影结果类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="selector">接收七个元素的投影表达式。</param>
        /// <returns>投影后的查询。</returns>
        public static IQueryable<TResult> Select<T1, T2, T3, T4, T5, T6, T7, TResult>(
            this IQueryable<(T1, T2, T3, T4, T5, T6, T7)> source,
            Expression<Func<T1, T2, T3, T4, T5, T6, T7, TResult>> selector)
            => SelectCore<(T1, T2, T3, T4, T5, T6, T7), TResult>(source, selector);

        #endregion

        #region OrderBy

        /// <summary>使用两个独立参数按指定键升序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="keySelector">接收两个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2)> OrderBy<T1, T2, TKey>(
            this IQueryable<(T1, T2)> source,
            Expression<Func<T1, T2, TKey>> keySelector)
            => OrderByCore<(T1, T2), TKey>(source, keySelector, ascending: true);

        /// <summary>使用三个独立参数按指定键升序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="keySelector">接收三个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2, T3)> OrderBy<T1, T2, T3, TKey>(
            this IQueryable<(T1, T2, T3)> source,
            Expression<Func<T1, T2, T3, TKey>> keySelector)
            => OrderByCore<(T1, T2, T3), TKey>(source, keySelector, ascending: true);

        /// <summary>使用四个独立参数按指定键升序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="keySelector">接收四个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2, T3, T4)> OrderBy<T1, T2, T3, T4, TKey>(
            this IQueryable<(T1, T2, T3, T4)> source,
            Expression<Func<T1, T2, T3, T4, TKey>> keySelector)
            => OrderByCore<(T1, T2, T3, T4), TKey>(source, keySelector, ascending: true);

        /// <summary>使用五个独立参数按指定键升序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="T5">元组第五个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="keySelector">接收五个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2, T3, T4, T5)> OrderBy<T1, T2, T3, T4, T5, TKey>(
            this IQueryable<(T1, T2, T3, T4, T5)> source,
            Expression<Func<T1, T2, T3, T4, T5, TKey>> keySelector)
            => OrderByCore<(T1, T2, T3, T4, T5), TKey>(source, keySelector, ascending: true);

        /// <summary>使用六个独立参数按指定键升序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="T5">元组第五个元素的类型。</typeparam>
        /// <typeparam name="T6">元组第六个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="keySelector">接收六个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2, T3, T4, T5, T6)> OrderBy<T1, T2, T3, T4, T5, T6, TKey>(
            this IQueryable<(T1, T2, T3, T4, T5, T6)> source,
            Expression<Func<T1, T2, T3, T4, T5, T6, TKey>> keySelector)
            => OrderByCore<(T1, T2, T3, T4, T5, T6), TKey>(source, keySelector, ascending: true);

        /// <summary>使用七个独立参数按指定键升序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="T5">元组第五个元素的类型。</typeparam>
        /// <typeparam name="T6">元组第六个元素的类型。</typeparam>
        /// <typeparam name="T7">元组第七个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="keySelector">接收七个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2, T3, T4, T5, T6, T7)> OrderBy<T1, T2, T3, T4, T5, T6, T7, TKey>(
            this IQueryable<(T1, T2, T3, T4, T5, T6, T7)> source,
            Expression<Func<T1, T2, T3, T4, T5, T6, T7, TKey>> keySelector)
            => OrderByCore<(T1, T2, T3, T4, T5, T6, T7), TKey>(source, keySelector, ascending: true);

        #endregion

        #region OrderByDescending

        /// <summary>使用两个独立参数按指定键降序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="keySelector">接收两个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2)> OrderByDescending<T1, T2, TKey>(
            this IQueryable<(T1, T2)> source,
            Expression<Func<T1, T2, TKey>> keySelector)
            => OrderByCore<(T1, T2), TKey>(source, keySelector, ascending: false);

        /// <summary>使用三个独立参数按指定键降序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="keySelector">接收三个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2, T3)> OrderByDescending<T1, T2, T3, TKey>(
            this IQueryable<(T1, T2, T3)> source,
            Expression<Func<T1, T2, T3, TKey>> keySelector)
            => OrderByCore<(T1, T2, T3), TKey>(source, keySelector, ascending: false);

        /// <summary>使用四个独立参数按指定键降序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="keySelector">接收四个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2, T3, T4)> OrderByDescending<T1, T2, T3, T4, TKey>(
            this IQueryable<(T1, T2, T3, T4)> source,
            Expression<Func<T1, T2, T3, T4, TKey>> keySelector)
            => OrderByCore<(T1, T2, T3, T4), TKey>(source, keySelector, ascending: false);

        /// <summary>使用五个独立参数按指定键降序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="T5">元组第五个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="keySelector">接收五个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2, T3, T4, T5)> OrderByDescending<T1, T2, T3, T4, T5, TKey>(
            this IQueryable<(T1, T2, T3, T4, T5)> source,
            Expression<Func<T1, T2, T3, T4, T5, TKey>> keySelector)
            => OrderByCore<(T1, T2, T3, T4, T5), TKey>(source, keySelector, ascending: false);

        /// <summary>使用六个独立参数按指定键降序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="T5">元组第五个元素的类型。</typeparam>
        /// <typeparam name="T6">元组第六个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="keySelector">接收六个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2, T3, T4, T5, T6)> OrderByDescending<T1, T2, T3, T4, T5, T6, TKey>(
            this IQueryable<(T1, T2, T3, T4, T5, T6)> source,
            Expression<Func<T1, T2, T3, T4, T5, T6, TKey>> keySelector)
            => OrderByCore<(T1, T2, T3, T4, T5, T6), TKey>(source, keySelector, ascending: false);

        /// <summary>使用七个独立参数按指定键降序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="T5">元组第五个元素的类型。</typeparam>
        /// <typeparam name="T6">元组第六个元素的类型。</typeparam>
        /// <typeparam name="T7">元组第七个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="keySelector">接收七个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2, T3, T4, T5, T6, T7)> OrderByDescending<T1, T2, T3, T4, T5, T6, T7, TKey>(
            this IQueryable<(T1, T2, T3, T4, T5, T6, T7)> source,
            Expression<Func<T1, T2, T3, T4, T5, T6, T7, TKey>> keySelector)
            => OrderByCore<(T1, T2, T3, T4, T5, T6, T7), TKey>(source, keySelector, ascending: false);

        #endregion

        #region ThenBy

        /// <summary>使用两个独立参数追加升序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">已排序的元组查询源。</param>
        /// <param name="keySelector">接收两个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2)> ThenBy<T1, T2, TKey>(
            this IOrderedQueryable<(T1, T2)> source,
            Expression<Func<T1, T2, TKey>> keySelector)
            => ThenByCore<(T1, T2), TKey>(source, keySelector, ascending: true);

        /// <summary>使用三个独立参数追加升序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">已排序的元组查询源。</param>
        /// <param name="keySelector">接收三个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2, T3)> ThenBy<T1, T2, T3, TKey>(
            this IOrderedQueryable<(T1, T2, T3)> source,
            Expression<Func<T1, T2, T3, TKey>> keySelector)
            => ThenByCore<(T1, T2, T3), TKey>(source, keySelector, ascending: true);

        /// <summary>使用四个独立参数追加升序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">已排序的元组查询源。</param>
        /// <param name="keySelector">接收四个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2, T3, T4)> ThenBy<T1, T2, T3, T4, TKey>(
            this IOrderedQueryable<(T1, T2, T3, T4)> source,
            Expression<Func<T1, T2, T3, T4, TKey>> keySelector)
            => ThenByCore<(T1, T2, T3, T4), TKey>(source, keySelector, ascending: true);

        /// <summary>使用五个独立参数追加升序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="T5">元组第五个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">已排序的元组查询源。</param>
        /// <param name="keySelector">接收五个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2, T3, T4, T5)> ThenBy<T1, T2, T3, T4, T5, TKey>(
            this IOrderedQueryable<(T1, T2, T3, T4, T5)> source,
            Expression<Func<T1, T2, T3, T4, T5, TKey>> keySelector)
            => ThenByCore<(T1, T2, T3, T4, T5), TKey>(source, keySelector, ascending: true);

        /// <summary>使用六个独立参数追加升序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="T5">元组第五个元素的类型。</typeparam>
        /// <typeparam name="T6">元组第六个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">已排序的元组查询源。</param>
        /// <param name="keySelector">接收六个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2, T3, T4, T5, T6)> ThenBy<T1, T2, T3, T4, T5, T6, TKey>(
            this IOrderedQueryable<(T1, T2, T3, T4, T5, T6)> source,
            Expression<Func<T1, T2, T3, T4, T5, T6, TKey>> keySelector)
            => ThenByCore<(T1, T2, T3, T4, T5, T6), TKey>(source, keySelector, ascending: true);

        /// <summary>使用七个独立参数追加升序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="T5">元组第五个元素的类型。</typeparam>
        /// <typeparam name="T6">元组第六个元素的类型。</typeparam>
        /// <typeparam name="T7">元组第七个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">已排序的元组查询源。</param>
        /// <param name="keySelector">接收七个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2, T3, T4, T5, T6, T7)> ThenBy<T1, T2, T3, T4, T5, T6, T7, TKey>(
            this IOrderedQueryable<(T1, T2, T3, T4, T5, T6, T7)> source,
            Expression<Func<T1, T2, T3, T4, T5, T6, T7, TKey>> keySelector)
            => ThenByCore<(T1, T2, T3, T4, T5, T6, T7), TKey>(source, keySelector, ascending: true);

        #endregion

        #region ThenByDescending

        /// <summary>使用两个独立参数追加降序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">已排序的元组查询源。</param>
        /// <param name="keySelector">接收两个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2)> ThenByDescending<T1, T2, TKey>(
            this IOrderedQueryable<(T1, T2)> source,
            Expression<Func<T1, T2, TKey>> keySelector)
            => ThenByCore<(T1, T2), TKey>(source, keySelector, ascending: false);

        /// <summary>使用三个独立参数追加降序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">已排序的元组查询源。</param>
        /// <param name="keySelector">接收三个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2, T3)> ThenByDescending<T1, T2, T3, TKey>(
            this IOrderedQueryable<(T1, T2, T3)> source,
            Expression<Func<T1, T2, T3, TKey>> keySelector)
            => ThenByCore<(T1, T2, T3), TKey>(source, keySelector, ascending: false);

        /// <summary>使用四个独立参数追加降序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">已排序的元组查询源。</param>
        /// <param name="keySelector">接收四个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2, T3, T4)> ThenByDescending<T1, T2, T3, T4, TKey>(
            this IOrderedQueryable<(T1, T2, T3, T4)> source,
            Expression<Func<T1, T2, T3, T4, TKey>> keySelector)
            => ThenByCore<(T1, T2, T3, T4), TKey>(source, keySelector, ascending: false);

        /// <summary>使用五个独立参数追加降序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="T5">元组第五个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">已排序的元组查询源。</param>
        /// <param name="keySelector">接收五个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2, T3, T4, T5)> ThenByDescending<T1, T2, T3, T4, T5, TKey>(
            this IOrderedQueryable<(T1, T2, T3, T4, T5)> source,
            Expression<Func<T1, T2, T3, T4, T5, TKey>> keySelector)
            => ThenByCore<(T1, T2, T3, T4, T5), TKey>(source, keySelector, ascending: false);

        /// <summary>使用六个独立参数追加降序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="T5">元组第五个元素的类型。</typeparam>
        /// <typeparam name="T6">元组第六个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">已排序的元组查询源。</param>
        /// <param name="keySelector">接收六个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2, T3, T4, T5, T6)> ThenByDescending<T1, T2, T3, T4, T5, T6, TKey>(
            this IOrderedQueryable<(T1, T2, T3, T4, T5, T6)> source,
            Expression<Func<T1, T2, T3, T4, T5, T6, TKey>> keySelector)
            => ThenByCore<(T1, T2, T3, T4, T5, T6), TKey>(source, keySelector, ascending: false);

        /// <summary>使用七个独立参数追加降序排序。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="T5">元组第五个元素的类型。</typeparam>
        /// <typeparam name="T6">元组第六个元素的类型。</typeparam>
        /// <typeparam name="T7">元组第七个元素的类型。</typeparam>
        /// <typeparam name="TKey">排序键类型。</typeparam>
        /// <param name="source">已排序的元组查询源。</param>
        /// <param name="keySelector">接收七个元素的排序键表达式。</param>
        /// <returns>排序后的查询。</returns>
        public static IOrderedQueryable<(T1, T2, T3, T4, T5, T6, T7)> ThenByDescending<T1, T2, T3, T4, T5, T6, T7, TKey>(
            this IOrderedQueryable<(T1, T2, T3, T4, T5, T6, T7)> source,
            Expression<Func<T1, T2, T3, T4, T5, T6, T7, TKey>> keySelector)
            => ThenByCore<(T1, T2, T3, T4, T5, T6, T7), TKey>(source, keySelector, ascending: false);

        #endregion

        #region GroupBy

        /// <summary>使用两个独立参数按指定键分组。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="TKey">分组键类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="keySelector">接收两个元素的分组键表达式。</param>
        /// <returns>分组后的查询。</returns>
        public static IQueryable<IGrouping<TKey, (T1, T2)>> GroupBy<T1, T2, TKey>(
            this IQueryable<(T1, T2)> source,
            Expression<Func<T1, T2, TKey>> keySelector)
            => GroupByCore<(T1, T2), TKey>(source, keySelector);

        /// <summary>使用三个独立参数按指定键分组。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="TKey">分组键类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="keySelector">接收三个元素的分组键表达式。</param>
        /// <returns>分组后的查询。</returns>
        public static IQueryable<IGrouping<TKey, (T1, T2, T3)>> GroupBy<T1, T2, T3, TKey>(
            this IQueryable<(T1, T2, T3)> source,
            Expression<Func<T1, T2, T3, TKey>> keySelector)
            => GroupByCore<(T1, T2, T3), TKey>(source, keySelector);

        /// <summary>使用四个独立参数按指定键分组。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="TKey">分组键类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="keySelector">接收四个元素的分组键表达式。</param>
        /// <returns>分组后的查询。</returns>
        public static IQueryable<IGrouping<TKey, (T1, T2, T3, T4)>> GroupBy<T1, T2, T3, T4, TKey>(
            this IQueryable<(T1, T2, T3, T4)> source,
            Expression<Func<T1, T2, T3, T4, TKey>> keySelector)
            => GroupByCore<(T1, T2, T3, T4), TKey>(source, keySelector);

        /// <summary>使用五个独立参数按指定键分组。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="T5">元组第五个元素的类型。</typeparam>
        /// <typeparam name="TKey">分组键类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="keySelector">接收五个元素的分组键表达式。</param>
        /// <returns>分组后的查询。</returns>
        public static IQueryable<IGrouping<TKey, (T1, T2, T3, T4, T5)>> GroupBy<T1, T2, T3, T4, T5, TKey>(
            this IQueryable<(T1, T2, T3, T4, T5)> source,
            Expression<Func<T1, T2, T3, T4, T5, TKey>> keySelector)
            => GroupByCore<(T1, T2, T3, T4, T5), TKey>(source, keySelector);

        /// <summary>使用六个独立参数按指定键分组。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="T5">元组第五个元素的类型。</typeparam>
        /// <typeparam name="T6">元组第六个元素的类型。</typeparam>
        /// <typeparam name="TKey">分组键类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="keySelector">接收六个元素的分组键表达式。</param>
        /// <returns>分组后的查询。</returns>
        public static IQueryable<IGrouping<TKey, (T1, T2, T3, T4, T5, T6)>> GroupBy<T1, T2, T3, T4, T5, T6, TKey>(
            this IQueryable<(T1, T2, T3, T4, T5, T6)> source,
            Expression<Func<T1, T2, T3, T4, T5, T6, TKey>> keySelector)
            => GroupByCore<(T1, T2, T3, T4, T5, T6), TKey>(source, keySelector);

        /// <summary>使用七个独立参数按指定键分组。</summary>
        /// <typeparam name="T1">元组第一个元素的类型。</typeparam>
        /// <typeparam name="T2">元组第二个元素的类型。</typeparam>
        /// <typeparam name="T3">元组第三个元素的类型。</typeparam>
        /// <typeparam name="T4">元组第四个元素的类型。</typeparam>
        /// <typeparam name="T5">元组第五个元素的类型。</typeparam>
        /// <typeparam name="T6">元组第六个元素的类型。</typeparam>
        /// <typeparam name="T7">元组第七个元素的类型。</typeparam>
        /// <typeparam name="TKey">分组键类型。</typeparam>
        /// <param name="source">元组查询源。</param>
        /// <param name="keySelector">接收七个元素的分组键表达式。</param>
        /// <returns>分组后的查询。</returns>
        public static IQueryable<IGrouping<TKey, (T1, T2, T3, T4, T5, T6, T7)>> GroupBy<T1, T2, T3, T4, T5, T6, T7, TKey>(
            this IQueryable<(T1, T2, T3, T4, T5, T6, T7)> source,
            Expression<Func<T1, T2, T3, T4, T5, T6, T7, TKey>> keySelector)
            => GroupByCore<(T1, T2, T3, T4, T5, T6, T7), TKey>(source, keySelector);

        #endregion

        #region Core

        private static IQueryable<TResult> SelectCore<TTuple, TResult>(
            IQueryable<TTuple> source,
            LambdaExpression selector)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (selector is null) throw new ArgumentNullException(nameof(selector));

            return Queryable.Select(source, RewriteTupleLambda<TTuple, TResult>(selector));
        }

        private static IOrderedQueryable<TTuple> OrderByCore<TTuple, TKey>(
            IQueryable<TTuple> source,
            LambdaExpression keySelector,
            bool ascending)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (keySelector is null) throw new ArgumentNullException(nameof(keySelector));

            var key = RewriteTupleLambda<TTuple, TKey>(keySelector);
            return ascending ? Queryable.OrderBy(source, key) : Queryable.OrderByDescending(source, key);
        }

        private static IOrderedQueryable<TTuple> ThenByCore<TTuple, TKey>(
            IOrderedQueryable<TTuple> source,
            LambdaExpression keySelector,
            bool ascending)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (keySelector is null) throw new ArgumentNullException(nameof(keySelector));

            var key = RewriteTupleLambda<TTuple, TKey>(keySelector);
            return ascending ? Queryable.ThenBy(source, key) : Queryable.ThenByDescending(source, key);
        }

        private static IQueryable<IGrouping<TKey, TTuple>> GroupByCore<TTuple, TKey>(
            IQueryable<TTuple> source,
            LambdaExpression keySelector)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (keySelector is null) throw new ArgumentNullException(nameof(keySelector));

            return Queryable.GroupBy(source, RewriteTupleLambda<TTuple, TKey>(keySelector));
        }

        #endregion
    }
}
