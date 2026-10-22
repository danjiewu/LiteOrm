using System;
using System.Linq.Expressions;

namespace LiteOrm.Common
{
    /// <summary>
    /// 双表查询源。元素类型为 <c>(T1, T2)</c>，可直接使用 Where / Select / OrderBy 等查询算子，
    /// 也可调用 <c>Join&lt;T3&gt;</c> 继续追加连接。
    /// </summary>
    /// <typeparam name="T1">第一张表实体类型。</typeparam>
    /// <typeparam name="T2">第二张表实体类型。</typeparam>
    public sealed class QuerySource<T1, T2> : QuerySource<(T1, T2)>
    {
        /// <summary>
        /// 无执行器构造：仅用于拼装表达式。
        /// </summary>
        public QuerySource() { }

        /// <summary>
        /// 带执行器构造。
        /// </summary>
        /// <param name="executor">查询执行器。</param>
        public QuerySource(IQuerySourceExecutor<(T1, T2)> executor) : base(executor) { }

        internal QuerySource(IQuerySourceExecutor? executor, Expression? expression) : base(executor, expression) { }

        /// <summary>
        /// 连接第三张表，并使用显式连接条件。
        /// </summary>
        /// <typeparam name="T3">右表实体类型。</typeparam>
        /// <param name="on">连接条件表达式，第一个参数为前两张表的元组。</param>
        /// <param name="joinType">连接类型，为 null 时使用左连接。</param>
        /// <returns>元素为三表元组的查询源。</returns>
        public new QuerySource<T1, T2, T3> Join<T3>(Expression<Func<(T1, T2), T3, bool>> on, TableJoinType? joinType = null)
        {
            if (on is null) throw new ArgumentNullException(nameof(on));
            Func<Expression<Func<(T1, T2), T3, bool>>, TableJoinType?, QuerySource<T1, T2, T3>> target = Join;
            return new QuerySource<T1, T2, T3>(Executor,
                QuerySourceExpressionBuilder.BuildJoinCall(QueryExpression, target.Method, Expression.Quote(on), QuerySourceExpressionBuilder.JoinTypeArgument(joinType)));
        }

        /// <summary>
        /// 连接第三张表，并按外键元数据自动推断连接条件。
        /// </summary>
        /// <typeparam name="T3">右表实体类型。</typeparam>
        /// <param name="joinType">连接类型，为 null 时沿用元数据中声明的连接方式。</param>
        /// <returns>元素为三表元组的查询源。</returns>
        public new QuerySource<T1, T2, T3> JoinRelated<T3>(TableJoinType? joinType = null)
        {
            Func<TableJoinType?, QuerySource<T1, T2, T3>> target =
                new Func<TableJoinType?, QuerySource<T1, T2, T3>>(JoinRelated<T3>);
            return new QuerySource<T1, T2, T3>(Executor,
                QuerySourceExpressionBuilder.BuildJoinCall(QueryExpression, target.Method, QuerySourceExpressionBuilder.JoinTypeArgument(joinType)));
        }

        /// <summary>
        /// 连接第三张表，自动推断的连接条件与显式条件按 AND 合并。
        /// </summary>
        /// <typeparam name="T3">右表实体类型。</typeparam>
        /// <param name="on">附加的连接条件，第一个参数为前两张表的元组。</param>
        /// <param name="joinType">连接类型，为 null 时沿用元数据中声明的连接方式。</param>
        /// <returns>元素为三表元组的查询源。</returns>
        public new QuerySource<T1, T2, T3> JoinRelated<T3>(Expression<Func<(T1, T2), T3, bool>> on, TableJoinType? joinType = null)
        {
            if (on is null) throw new ArgumentNullException(nameof(on));
            Func<Expression<Func<(T1, T2), T3, bool>>, TableJoinType?, QuerySource<T1, T2, T3>> target = JoinRelated<T3>;
            return new QuerySource<T1, T2, T3>(Executor,
                QuerySourceExpressionBuilder.BuildJoinCall(QueryExpression, target.Method, Expression.Quote(on), QuerySourceExpressionBuilder.JoinTypeArgument(joinType)));
        }
    }

    /// <summary>
    /// 三表查询源。元素类型为 <c>(T1, T2, T3)</c>，可调用 <c>Join&lt;T4&gt;</c> 继续追加连接。
    /// </summary>
    /// <typeparam name="T1">第一张表实体类型。</typeparam>
    /// <typeparam name="T2">第二张表实体类型。</typeparam>
    /// <typeparam name="T3">第三张表实体类型。</typeparam>
    public sealed class QuerySource<T1, T2, T3> : QuerySource<(T1, T2, T3)>
    {
        /// <summary>
        /// 无执行器构造：仅用于拼装表达式。
        /// </summary>
        public QuerySource() { }

        /// <summary>
        /// 带执行器构造。
        /// </summary>
        /// <param name="executor">查询执行器。</param>
        public QuerySource(IQuerySourceExecutor<(T1, T2, T3)> executor) : base(executor) { }

        internal QuerySource(IQuerySourceExecutor? executor, Expression? expression) : base(executor, expression) { }

        /// <summary>
        /// 连接第四张表，并使用显式连接条件。
        /// </summary>
        /// <typeparam name="T4">右表实体类型。</typeparam>
        /// <param name="on">连接条件表达式，第一个参数为前三张表的元组。</param>
        /// <param name="joinType">连接类型，为 null 时使用左连接。</param>
        /// <returns>元素为四表元组的查询源。</returns>
        public new QuerySource<T1, T2, T3, T4> Join<T4>(Expression<Func<(T1, T2, T3), T4, bool>> on, TableJoinType? joinType = null)
        {
            if (on is null) throw new ArgumentNullException(nameof(on));
            Func<Expression<Func<(T1, T2, T3), T4, bool>>, TableJoinType?, QuerySource<T1, T2, T3, T4>> target = Join;
            return new QuerySource<T1, T2, T3, T4>(Executor,
                QuerySourceExpressionBuilder.BuildJoinCall(QueryExpression, target.Method, Expression.Quote(on), QuerySourceExpressionBuilder.JoinTypeArgument(joinType)));
        }

        /// <summary>
        /// 连接第四张表，并按外键元数据自动推断连接条件。
        /// </summary>
        /// <typeparam name="T4">右表实体类型。</typeparam>
        /// <param name="joinType">连接类型，为 null 时沿用元数据中声明的连接方式。</param>
        /// <returns>元素为四表元组的查询源。</returns>
        public new QuerySource<T1, T2, T3, T4> JoinRelated<T4>(TableJoinType? joinType = null)
        {
            Func<TableJoinType?, QuerySource<T1, T2, T3, T4>> target =
                new Func<TableJoinType?, QuerySource<T1, T2, T3, T4>>(JoinRelated<T4>);
            return new QuerySource<T1, T2, T3, T4>(Executor,
                QuerySourceExpressionBuilder.BuildJoinCall(QueryExpression, target.Method, QuerySourceExpressionBuilder.JoinTypeArgument(joinType)));
        }

        /// <summary>
        /// 连接第四张表，自动推断的连接条件与显式条件按 AND 合并。
        /// </summary>
        /// <typeparam name="T4">右表实体类型。</typeparam>
        /// <param name="on">附加的连接条件，第一个参数为前三张表的元组。</param>
        /// <param name="joinType">连接类型，为 null 时沿用元数据中声明的连接方式。</param>
        /// <returns>元素为四表元组的查询源。</returns>
        public new QuerySource<T1, T2, T3, T4> JoinRelated<T4>(Expression<Func<(T1, T2, T3), T4, bool>> on, TableJoinType? joinType = null)
        {
            if (on is null) throw new ArgumentNullException(nameof(on));
            Func<Expression<Func<(T1, T2, T3), T4, bool>>, TableJoinType?, QuerySource<T1, T2, T3, T4>> target = JoinRelated<T4>;
            return new QuerySource<T1, T2, T3, T4>(Executor,
                QuerySourceExpressionBuilder.BuildJoinCall(QueryExpression, target.Method, Expression.Quote(on), QuerySourceExpressionBuilder.JoinTypeArgument(joinType)));
        }
    }

    /// <summary>
    /// 四表查询源。元素类型为 <c>(T1, T2, T3, T4)</c>，可调用 <c>Join&lt;T5&gt;</c> 继续追加连接。
    /// </summary>
    /// <typeparam name="T1">第一张表实体类型。</typeparam>
    /// <typeparam name="T2">第二张表实体类型。</typeparam>
    /// <typeparam name="T3">第三张表实体类型。</typeparam>
    /// <typeparam name="T4">第四张表实体类型。</typeparam>
    public sealed class QuerySource<T1, T2, T3, T4> : QuerySource<(T1, T2, T3, T4)>
    {
        /// <summary>
        /// 无执行器构造：仅用于拼装表达式。
        /// </summary>
        public QuerySource() { }

        /// <summary>
        /// 带执行器构造。
        /// </summary>
        /// <param name="executor">查询执行器。</param>
        public QuerySource(IQuerySourceExecutor<(T1, T2, T3, T4)> executor) : base(executor) { }

        internal QuerySource(IQuerySourceExecutor? executor, Expression? expression) : base(executor, expression) { }

        /// <summary>
        /// 连接第五张表，并使用显式连接条件。
        /// </summary>
        /// <typeparam name="T5">右表实体类型。</typeparam>
        /// <param name="on">连接条件表达式，第一个参数为前四张表的元组。</param>
        /// <param name="joinType">连接类型，为 null 时使用左连接。</param>
        /// <returns>元素为五表元组的查询源。</returns>
        public new QuerySource<T1, T2, T3, T4, T5> Join<T5>(Expression<Func<(T1, T2, T3, T4), T5, bool>> on, TableJoinType? joinType = null)
        {
            if (on is null) throw new ArgumentNullException(nameof(on));
            Func<Expression<Func<(T1, T2, T3, T4), T5, bool>>, TableJoinType?, QuerySource<T1, T2, T3, T4, T5>> target = Join;
            return new QuerySource<T1, T2, T3, T4, T5>(Executor,
                QuerySourceExpressionBuilder.BuildJoinCall(QueryExpression, target.Method, Expression.Quote(on), QuerySourceExpressionBuilder.JoinTypeArgument(joinType)));
        }

        /// <summary>
        /// 连接第五张表，并按外键元数据自动推断连接条件。
        /// </summary>
        /// <typeparam name="T5">右表实体类型。</typeparam>
        /// <param name="joinType">连接类型，为 null 时沿用元数据中声明的连接方式。</param>
        /// <returns>元素为五表元组的查询源。</returns>
        public new QuerySource<T1, T2, T3, T4, T5> JoinRelated<T5>(TableJoinType? joinType = null)
        {
            Func<TableJoinType?, QuerySource<T1, T2, T3, T4, T5>> target =
                new Func<TableJoinType?, QuerySource<T1, T2, T3, T4, T5>>(JoinRelated<T5>);
            return new QuerySource<T1, T2, T3, T4, T5>(Executor,
                QuerySourceExpressionBuilder.BuildJoinCall(QueryExpression, target.Method, QuerySourceExpressionBuilder.JoinTypeArgument(joinType)));
        }

        /// <summary>
        /// 连接第五张表，自动推断的连接条件与显式条件按 AND 合并。
        /// </summary>
        /// <typeparam name="T5">右表实体类型。</typeparam>
        /// <param name="on">附加的连接条件，第一个参数为前四张表的元组。</param>
        /// <param name="joinType">连接类型，为 null 时沿用元数据中声明的连接方式。</param>
        /// <returns>元素为五表元组的查询源。</returns>
        public new QuerySource<T1, T2, T3, T4, T5> JoinRelated<T5>(Expression<Func<(T1, T2, T3, T4), T5, bool>> on, TableJoinType? joinType = null)
        {
            if (on is null) throw new ArgumentNullException(nameof(on));
            Func<Expression<Func<(T1, T2, T3, T4), T5, bool>>, TableJoinType?, QuerySource<T1, T2, T3, T4, T5>> target = JoinRelated<T5>;
            return new QuerySource<T1, T2, T3, T4, T5>(Executor,
                QuerySourceExpressionBuilder.BuildJoinCall(QueryExpression, target.Method, Expression.Quote(on), QuerySourceExpressionBuilder.JoinTypeArgument(joinType)));
        }
    }

    /// <summary>
    /// 五表查询源。元素类型为 <c>(T1, T2, T3, T4, T5)</c>，可调用 <c>Join&lt;T6&gt;</c> 继续追加连接。
    /// </summary>
    /// <typeparam name="T1">第一张表实体类型。</typeparam>
    /// <typeparam name="T2">第二张表实体类型。</typeparam>
    /// <typeparam name="T3">第三张表实体类型。</typeparam>
    /// <typeparam name="T4">第四张表实体类型。</typeparam>
    /// <typeparam name="T5">第五张表实体类型。</typeparam>
    public sealed class QuerySource<T1, T2, T3, T4, T5> : QuerySource<(T1, T2, T3, T4, T5)>
    {
        /// <summary>
        /// 无执行器构造：仅用于拼装表达式。
        /// </summary>
        public QuerySource() { }

        /// <summary>
        /// 带执行器构造。
        /// </summary>
        /// <param name="executor">查询执行器。</param>
        public QuerySource(IQuerySourceExecutor<(T1, T2, T3, T4, T5)> executor) : base(executor) { }

        internal QuerySource(IQuerySourceExecutor? executor, Expression? expression) : base(executor, expression) { }

        /// <summary>
        /// 连接第六张表，并使用显式连接条件。
        /// </summary>
        /// <typeparam name="T6">右表实体类型。</typeparam>
        /// <param name="on">连接条件表达式，第一个参数为前五张表的元组。</param>
        /// <param name="joinType">连接类型，为 null 时使用左连接。</param>
        /// <returns>元素为六表元组的查询源。</returns>
        public new QuerySource<T1, T2, T3, T4, T5, T6> Join<T6>(Expression<Func<(T1, T2, T3, T4, T5), T6, bool>> on, TableJoinType? joinType = null)
        {
            if (on is null) throw new ArgumentNullException(nameof(on));
            Func<Expression<Func<(T1, T2, T3, T4, T5), T6, bool>>, TableJoinType?, QuerySource<T1, T2, T3, T4, T5, T6>> target = Join;
            return new QuerySource<T1, T2, T3, T4, T5, T6>(Executor,
                QuerySourceExpressionBuilder.BuildJoinCall(QueryExpression, target.Method, Expression.Quote(on), QuerySourceExpressionBuilder.JoinTypeArgument(joinType)));
        }

        /// <summary>
        /// 连接第六张表，并按外键元数据自动推断连接条件。
        /// </summary>
        /// <typeparam name="T6">右表实体类型。</typeparam>
        /// <param name="joinType">连接类型，为 null 时沿用元数据中声明的连接方式。</param>
        /// <returns>元素为六表元组的查询源。</returns>
        public new QuerySource<T1, T2, T3, T4, T5, T6> JoinRelated<T6>(TableJoinType? joinType = null)
        {
            Func<TableJoinType?, QuerySource<T1, T2, T3, T4, T5, T6>> target =
                new Func<TableJoinType?, QuerySource<T1, T2, T3, T4, T5, T6>>(JoinRelated<T6>);
            return new QuerySource<T1, T2, T3, T4, T5, T6>(Executor,
                QuerySourceExpressionBuilder.BuildJoinCall(QueryExpression, target.Method, QuerySourceExpressionBuilder.JoinTypeArgument(joinType)));
        }

        /// <summary>
        /// 连接第六张表，自动推断的连接条件与显式条件按 AND 合并。
        /// </summary>
        /// <typeparam name="T6">右表实体类型。</typeparam>
        /// <param name="on">附加的连接条件，第一个参数为前五张表的元组。</param>
        /// <param name="joinType">连接类型，为 null 时沿用元数据中声明的连接方式。</param>
        /// <returns>元素为六表元组的查询源。</returns>
        public new QuerySource<T1, T2, T3, T4, T5, T6> JoinRelated<T6>(Expression<Func<(T1, T2, T3, T4, T5), T6, bool>> on, TableJoinType? joinType = null)
        {
            if (on is null) throw new ArgumentNullException(nameof(on));
            Func<Expression<Func<(T1, T2, T3, T4, T5), T6, bool>>, TableJoinType?, QuerySource<T1, T2, T3, T4, T5, T6>> target = JoinRelated<T6>;
            return new QuerySource<T1, T2, T3, T4, T5, T6>(Executor,
                QuerySourceExpressionBuilder.BuildJoinCall(QueryExpression, target.Method, Expression.Quote(on), QuerySourceExpressionBuilder.JoinTypeArgument(joinType)));
        }
    }

    /// <summary>
    /// 六表查询源。元素类型为 <c>(T1, T2, T3, T4, T5, T6)</c>，可调用 <c>Join&lt;T7&gt;</c> 追加最后一张表。
    /// </summary>
    /// <typeparam name="T1">第一张表实体类型。</typeparam>
    /// <typeparam name="T2">第二张表实体类型。</typeparam>
    /// <typeparam name="T3">第三张表实体类型。</typeparam>
    /// <typeparam name="T4">第四张表实体类型。</typeparam>
    /// <typeparam name="T5">第五张表实体类型。</typeparam>
    /// <typeparam name="T6">第六张表实体类型。</typeparam>
    public sealed class QuerySource<T1, T2, T3, T4, T5, T6> : QuerySource<(T1, T2, T3, T4, T5, T6)>
    {
        /// <summary>
        /// 无执行器构造：仅用于拼装表达式。
        /// </summary>
        public QuerySource() { }

        /// <summary>
        /// 带执行器构造。
        /// </summary>
        /// <param name="executor">查询执行器。</param>
        public QuerySource(IQuerySourceExecutor<(T1, T2, T3, T4, T5, T6)> executor) : base(executor) { }

        internal QuerySource(IQuerySourceExecutor? executor, Expression? expression) : base(executor, expression) { }

        /// <summary>
        /// 连接第七张表，并使用显式连接条件。
        /// </summary>
        /// <typeparam name="T7">右表实体类型。</typeparam>
        /// <param name="on">连接条件表达式，第一个参数为前六张表的元组。</param>
        /// <param name="joinType">连接类型，为 null 时使用左连接。</param>
        /// <returns>元素为七表元组的查询源。</returns>
        public new QuerySource<T1, T2, T3, T4, T5, T6, T7> Join<T7>(Expression<Func<(T1, T2, T3, T4, T5, T6), T7, bool>> on, TableJoinType? joinType = null)
        {
            if (on is null) throw new ArgumentNullException(nameof(on));
            Func<Expression<Func<(T1, T2, T3, T4, T5, T6), T7, bool>>, TableJoinType?, QuerySource<T1, T2, T3, T4, T5, T6, T7>> target = Join;
            return new QuerySource<T1, T2, T3, T4, T5, T6, T7>(Executor,
                QuerySourceExpressionBuilder.BuildJoinCall(QueryExpression, target.Method, Expression.Quote(on), QuerySourceExpressionBuilder.JoinTypeArgument(joinType)));
        }

        /// <summary>
        /// 连接第七张表，并按外键元数据自动推断连接条件。
        /// </summary>
        /// <typeparam name="T7">右表实体类型。</typeparam>
        /// <param name="joinType">连接类型，为 null 时沿用元数据中声明的连接方式。</param>
        /// <returns>元素为七表元组的查询源。</returns>
        public new QuerySource<T1, T2, T3, T4, T5, T6, T7> JoinRelated<T7>(TableJoinType? joinType = null)
        {
            Func<TableJoinType?, QuerySource<T1, T2, T3, T4, T5, T6, T7>> target =
                new Func<TableJoinType?, QuerySource<T1, T2, T3, T4, T5, T6, T7>>(JoinRelated<T7>);
            return new QuerySource<T1, T2, T3, T4, T5, T6, T7>(Executor,
                QuerySourceExpressionBuilder.BuildJoinCall(QueryExpression, target.Method, QuerySourceExpressionBuilder.JoinTypeArgument(joinType)));
        }

        /// <summary>
        /// 连接第七张表，自动推断的连接条件与显式条件按 AND 合并。
        /// </summary>
        /// <typeparam name="T7">右表实体类型。</typeparam>
        /// <param name="on">附加的连接条件，第一个参数为前六张表的元组。</param>
        /// <param name="joinType">连接类型，为 null 时沿用元数据中声明的连接方式。</param>
        /// <returns>元素为七表元组的查询源。</returns>
        public new QuerySource<T1, T2, T3, T4, T5, T6, T7> JoinRelated<T7>(Expression<Func<(T1, T2, T3, T4, T5, T6), T7, bool>> on, TableJoinType? joinType = null)
        {
            if (on is null) throw new ArgumentNullException(nameof(on));
            Func<Expression<Func<(T1, T2, T3, T4, T5, T6), T7, bool>>, TableJoinType?, QuerySource<T1, T2, T3, T4, T5, T6, T7>> target = JoinRelated<T7>;
            return new QuerySource<T1, T2, T3, T4, T5, T6, T7>(Executor,
                QuerySourceExpressionBuilder.BuildJoinCall(QueryExpression, target.Method, Expression.Quote(on), QuerySourceExpressionBuilder.JoinTypeArgument(joinType)));
        }
    }

    /// <summary>
    /// 七表查询源。元素类型为 <c>(T1, T2, T3, T4, T5, T6, T7)</c>，已达到单条 SELECT 支持的最大连接数。
    /// </summary>
    /// <typeparam name="T1">第一张表实体类型。</typeparam>
    /// <typeparam name="T2">第二张表实体类型。</typeparam>
    /// <typeparam name="T3">第三张表实体类型。</typeparam>
    /// <typeparam name="T4">第四张表实体类型。</typeparam>
    /// <typeparam name="T5">第五张表实体类型。</typeparam>
    /// <typeparam name="T6">第六张表实体类型。</typeparam>
    /// <typeparam name="T7">第七张表实体类型。</typeparam>
    public sealed class QuerySource<T1, T2, T3, T4, T5, T6, T7> : QuerySource<(T1, T2, T3, T4, T5, T6, T7)>
    {
        /// <summary>
        /// 无执行器构造：仅用于拼装表达式。
        /// </summary>
        public QuerySource() { }

        /// <summary>
        /// 带执行器构造。
        /// </summary>
        /// <param name="executor">查询执行器。</param>
        public QuerySource(IQuerySourceExecutor<(T1, T2, T3, T4, T5, T6, T7)> executor) : base(executor) { }

        internal QuerySource(IQuerySourceExecutor? executor, Expression? expression) : base(executor, expression) { }
    }
}
