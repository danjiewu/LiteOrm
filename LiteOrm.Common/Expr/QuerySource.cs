using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace LiteOrm.Common
{
    /// <summary>
    /// <see cref="Expr.From{T}(string[])"/> 的返回类型：把左表类型固定在类型参数上，
    /// 使 <see cref="JoinRelated{T2}()"/> 这类方法只需要声明右表类型。
    /// <typeparamref name="T1"/> 允许是元组类型（<c>(A, B)</c>、<c>((A, B), C)</c>），
    /// 连接结果仍是 <see cref="QuerySource{T}"/>，因此可以继续调用 <c>Join&lt;T3&gt;</c> 追加多级连接。
    /// 仅用于在 Lambda 表达式中构造查询，实例方法一律抛出异常，由解析器接管。
    /// </summary>
    /// <typeparam name="T1">左表实体类型，或多级连接时的左元组类型</typeparam>
    public sealed class QuerySource<T1> : IEnumerable<T1>
    {
        private QuerySource() { }

        /// <inheritdoc/>
        public IEnumerator<T1> GetEnumerator()
            => throw new InvalidOperationException("The query source is only used for parsing lambda expressions and cannot be enumerated.");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// 连接另一个实体表，并使用显式的连接条件。仅用于 Lambda 表达式中构造 Expr，无实际执行逻辑。
        /// </summary>
        /// <typeparam name="T2">右表实体类型。</typeparam>
        /// <param name="on">连接条件表达式。</param>
        /// <returns>连接结果查询源，元素为左右两表的元组，可继续 <c>Join&lt;T3&gt;</c> 追加多级连接。</returns>
        /// <example>
        /// <code>
        /// var query = Expr.Query&lt;Order, int&gt;(q =&gt; Expr.From&lt;Order&gt;()
        ///     .Join&lt;Customer&gt;((o, c) =&gt; o.CustomerId == c.Id)
        ///     .Where(x =&gt; x.Item2.Name == "A")
        ///     .Count());
        /// </code>
        /// </example>
        public QuerySource<(T1, T2)> Join<T2>(Expression<Func<T1, T2, bool>> on)
            => throw new InvalidOperationException("The Join method is only used for parsing lambda expressions and cannot be called directly.");

        /// <summary>
        /// 连接另一个实体表，并按外键元数据自动推断连接条件。仅用于 Lambda 表达式中构造 Expr，无实际执行逻辑。
        /// </summary>
        /// <typeparam name="T2">右表实体类型。</typeparam>
        /// <returns>连接结果查询源，元素为左右两表的元组，可继续 <c>Join&lt;T3&gt;</c> 追加多级连接。</returns>
        /// <example>
        /// <code>
        /// var query = Expr.Query&lt;Order, int&gt;(q =&gt; Expr.From&lt;Order&gt;()
        ///     .JoinRelated&lt;Customer&gt;()
        ///     .Count());
        /// </code>
        /// </example>
        public QuerySource<(T1, T2)> JoinRelated<T2>()
            => throw new InvalidOperationException("The JoinRelated method is only used for parsing lambda expressions and cannot be called directly.");

        /// <summary>
        /// 连接另一个实体表，自动推断的连接条件与显式条件按 AND 合并。仅用于 Lambda 表达式中构造 Expr，无实际执行逻辑。
        /// </summary>
        /// <typeparam name="T2">右表实体类型。</typeparam>
        /// <param name="on">附加的连接条件，与自动推断的关联条件取 AND。</param>
        /// <returns>连接结果查询源，元素为左右两表的元组，可继续 <c>Join&lt;T3&gt;</c> 追加多级连接。</returns>
        public QuerySource<(T1, T2)> JoinRelated<T2>(Expression<Func<T1, T2, bool>> on)
            => throw new InvalidOperationException("The JoinRelated method is only used for parsing lambda expressions and cannot be called directly.");
    }
}